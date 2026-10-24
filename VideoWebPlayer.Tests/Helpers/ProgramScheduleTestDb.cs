using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared in-memory SQLite database fixture for program-schedule tests: real SQLite with foreign
/// keys, plus seed helpers for users, media source access, shows/movies with probed durations and
/// program rules.
/// </summary>
internal sealed class ProgramScheduleTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    private ProgramScheduleTestDb(SqliteConnection connection, ApplicationDbContext db)
    {
        _connection = connection;
        Db = db;
    }

    public ApplicationDbContext Db { get; }

    public static async Task<ProgramScheduleTestDb> CreateAsync(string dbName, CancellationToken ct)
    {
        var connection = new SqliteConnection($"Data Source=file:{dbName}?mode=memory&cache=shared");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        // Feature-Flag: Tests der Sender-Funktionen laufen standardmaessig mit Freigabe;
        // Tests der Sperre setzen den Wert explizit auf false.
        db.Setups.Add(new Setup { TvSenderEnabled = true });
        await db.SaveChangesAsync(ct);
        return new ProgramScheduleTestDb(connection, db);
    }

    public ProgramScheduleService CreateService(Microsoft.Extensions.Options.IOptions<VideoWebPlayer.Configuration.ProgramScheduleOptions>? scheduleOptions = null)
        => new(Db, new ProgramRuleService(Db), new ProgramSettingsService(Db, NullLogger<ProgramSettingsService>.Instance), NullLogger<ProgramScheduleService>.Instance, scheduleOptions: scheduleOptions);

    public ProgramGuideService CreateGuideService()
        => new(Db);

    /// <summary>Creates a program slot directly in the database — the seed the guide tests need
    /// without going through schedule generation.</summary>
    public async Task<ProgramSlot> CreateSlotAsync(
        long channelId, DateTime startUtc, DateTime endUtc, long mediaItemId,
        long? movieId = null, long? episodeId = null, long? ruleId = null,
        ProgramRuleType ruleType = ProgramRuleType.MoviePrimeTime,
        ProgramSlotStatus status = ProgramSlotStatus.Scheduled)
    {
        var slot = new ProgramSlot
        {
            TVChannelId = channelId,
            Start = startUtc,
            End = endUtc,
            MediaItemId = mediaItemId,
            MovieId = movieId,
            TVShowEpisodeId = episodeId,
            ProgramRuleId = ruleId,
            RuleType = ruleType,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        Db.ProgramSlots.Add(slot);
        await SaveDiagAsync();
        return slot;
    }

    public async Task<ApplicationUser> AddUserAsync(string email, bool isAdmin = false)
    {
        var user = new ApplicationUser { Id = Guid.NewGuid().ToString("N"), UserName = email, Email = email, IsAdmin = isAdmin };
        Db.Users.Add(user);
        await SaveDiagAsync();
        return user;
    }

    public async Task<MediaSource> EnsureSourceAsync(long id = 1)
    {
        var source = await Db.MediaSources.FindAsync(id);
        if (source is not null)
            return source;
        source = new MediaSource { Id = id, Name = "Testquelle", Path = "/test", Host = "localhost", Port = 22 };
        Db.MediaSources.Add(source);
        await SaveDiagAsync();
        return source;
    }

    public async Task GrantSourceAccessAsync(string userId, long sourceId = 1)
    {
        await EnsureSourceAsync(sourceId);
        Db.MediaSourceUsers.Add(new MediaSourceUser { UserId = userId, MediaSourceId = sourceId });
        await SaveDiagAsync();
    }

    /// <summary>Creates a media collection + media item with a probed duration in the given source.</summary>
    public async Task<MediaItem> CreateMediaItemAsync(long sourceId = 1, TimeSpan? duration = null)
    {
        await EnsureSourceAsync(sourceId);
        var collection = new MediaCollection { Name = $"Collection-{Guid.NewGuid():N}", MediaSourceId = sourceId, Path = $"/{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        Db.MediaCollections.Add(collection);
        await SaveDiagAsync();
        var item = new MediaItem
        {
            Name = $"file-{Guid.NewGuid():N}.mkv",
            Path = $"{collection.Path}/file.mkv",
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow,
            Duration = duration,
            DurationProbedAt = DateTime.UtcNow
        };
        Db.MediaItems.Add(item);
        await SaveDiagAsync();
        return item;
    }

    /// <summary>Creates a media item with an explicitly set duration-probe state (seeded via
    /// <see cref="CreateMediaItemAsync(long, TimeSpan?)"/>, then the probe fields are
    /// overwritten — e.g. an exhausted attempt counter for the failed set).</summary>
    public async Task<MediaItem> SeedProbeItemAsync(
        string name, TimeSpan? duration, DateTime? probedAt, int attempts, CancellationToken ct)
    {
        var item = await CreateMediaItemAsync();
        item.Name = name;
        item.Duration = duration;
        item.DurationProbedAt = probedAt;
        item.DurationProbeAttempts = attempts;
        await Db.SaveChangesAsync(ct);
        return item;
    }

    /// <summary>Creates a movie linked to a media item with the given duration.</summary>
    public async Task<Movie> CreateMovieAsync(string name, long sourceId = 1, TimeSpan? duration = null, long? collectionId = null, DateTime? releaseDate = null)
    {
        await EnsureSourceAsync(sourceId);
        var movie = new Movie { Name = name, MediaSourceId = sourceId, MovieCollectionId = collectionId, CreatedAt = DateTime.UtcNow, ReleaseDate = releaseDate };
        Db.Movies.Add(movie);
        await SaveDiagAsync();
        var item = await CreateMediaItemAsync(sourceId, duration);
        Db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });
        await SaveDiagAsync();
        return movie;
    }

    /// <summary>Creates a show with one season and the given episodes (numbered 1..n, each with a probed media item).</summary>
    public async Task<(TVShow Show, TVShowSeason Season, List<TVShowEpisode> Episodes)> CreateShowAsync(string name, int episodeCount, long sourceId = 1, TimeSpan? duration = null)
    {
        await EnsureSourceAsync(sourceId);
        var show = new TVShow { Name = name, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
        Db.TVShows.Add(show);
        await SaveDiagAsync();
        var season = new TVShowSeason { Name = "Staffel 1", TVShowId = show.Id, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
        Db.TVShowSeasons.Add(season);
        await SaveDiagAsync();

        var episodes = new List<TVShowEpisode>();
        for (var i = 1; i <= episodeCount; i++)
        {
            var episode = new TVShowEpisode { Name = $"{name} E{i}", TVShowSeasonId = season.Id, Number = i, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            Db.TVShowEpisodes.Add(episode);
            await SaveDiagAsync();
            var item = await CreateMediaItemAsync(sourceId, duration ?? TimeSpan.FromMinutes(45));
            Db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisodeId = episode.Id, MediaItemId = item.Id });
            episodes.Add(episode);
        }
        await SaveDiagAsync();
        return (show, season, episodes);
    }

    /// <summary>Creates a genre row on the given source (optionally hidden, with alternate names
    /// and a seasonal window).</summary>
    public async Task<Genre> CreateGenreAsync(string name, long sourceId = 1, bool isHidden = false,
        IEnumerable<string>? alternateNames = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        await EnsureSourceAsync(sourceId);
        var genre = new Genre { Name = name, MediaSourceId = sourceId, IsHidden = isHidden, StartDate = startDate, EndDate = endDate };
        Db.Genres.Add(genre);
        await SaveDiagAsync();
        if (alternateNames is not null)
        {
            foreach (var alt in alternateNames)
                Db.GenreNames.Add(new GenreName { Name = alt, GenreId = genre.Id });
            await SaveDiagAsync();
        }
        return genre;
    }

    /// <summary>Links a genre to a movie (MovieGenre row).</summary>
    public async Task LinkMovieGenreAsync(long movieId, long genreId)
    {
        Db.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
        await SaveDiagAsync();
    }

    /// <summary>Links a genre to a TV show (TVShowGenre row).</summary>
    public async Task LinkShowGenreAsync(long showId, long genreId)
    {
        Db.TVShowGenres.Add(new TVShowGenre { TVShowId = showId, GenreId = genreId });
        await SaveDiagAsync();
    }

    /// <summary>Creates a program rule directly in the database (service-level validation bypassed).</summary>
    public async Task<ProgramRule> CreateRuleAsync(
        long channelId, string name, ProgramRuleType type, ProgramRuleRecurrence recurrence,
        TimeOnly timeOfDay, ProgramRuleMaterialType materialType, long materialId,
        int priority = 0, int? episodeCount = null, TimeSpan? slotDuration = null,
        DayOfWeek? dayOfWeek = null, DateOnly? singleDate = null, bool isActive = true,
        IEnumerable<long>? genreIds = null, IEnumerable<long>? mediaSourceIds = null)
    {
        var rule = new ProgramRule
        {
            TVChannelId = channelId,
            Name = name,
            RuleType = type,
            Recurrence = recurrence,
            DayOfWeek = dayOfWeek,
            SingleDate = singleDate,
            TimeOfDay = timeOfDay,
            EpisodeCount = episodeCount,
            SlotDuration = slotDuration,
            MaterialType = materialType,
            MaterialId = materialId,
            Priority = priority,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Db.ProgramRules.Add(rule);
        await SaveDiagAsync();
        if (genreIds is not null)
        {
            foreach (var genreId in genreIds.Distinct())
                Db.ProgramRuleGenres.Add(new ProgramRuleGenre { ProgramRuleId = rule.Id, GenreId = genreId });
            await SaveDiagAsync();
        }
        if (mediaSourceIds is not null)
        {
            foreach (var sourceId in mediaSourceIds.Distinct())
                Db.ProgramRuleMediaSources.Add(new ProgramRuleMediaSource { ProgramRuleId = rule.Id, MediaSourceId = sourceId });
            await SaveDiagAsync();
        }
        return rule;
    }

    public async Task<TVChannel> GetChannelAsync(CancellationToken ct)
        => await new ProgramRuleService(Db).GetOrCreateChannelAsync(ct);

    /// <summary>SaveChanges with a foreign-key diagnostic dump in the error message on failure.</summary>
    public async Task SaveDiagAsync()
    {
        try
        {
            await Db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var pending = Db.ChangeTracker.Entries()
                .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                .Select(e => e.Entity.GetType().Name + "(" + string.Join(",", e.Properties.Where(pr => pr.Metadata.IsForeignKey()).Select(pr => pr.Metadata.Name + "=" + (pr.CurrentValue?.ToString() ?? "null"))) + ")")
                .ToList();
            throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message + " || pending: " + string.Join(" ; ", pending), ex);
        }
    }

    public async Task<List<ProgramSlot>> SlotsAsync(CancellationToken ct)
        => await Db.ProgramSlots.AsNoTracking().OrderBy(s => s.Start).ToListAsync(ct);

    /// <summary>Asserts a reconcile run displaced nothing — the displaced-slot dump is built
    /// only on failure.</summary>
    public static async Task AssertNoDisplacedAsync(ProgramScheduleTestDb fixture, ProgramScheduleReconcileSummary summary, CancellationToken ct, string? context = null)
    {
        if (summary.SlotsDisplaced == 0)
            return;
        var dumped = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Displaced)
            .Select(s => $"{s.Start:dd.HH:mm}-{s.End:dd.HH:mm} rule={s.ProgramRuleId} type={s.RuleType} ep={s.TVShowEpisodeId} item={s.MediaItemId}")
            .ToList();
        Assert.Fail((context is null ? "Displaced: " : context + " — Displaced: ") + string.Join(" | ", dumped));
    }

    /// <summary>Asserts the slot chain covers [from, to] without gaps or overlaps.</summary>
    public static void AssertGapless(IReadOnlyList<ProgramSlot> slots, DateTime from, DateTime to)
    {
        Assert.NotEmpty(slots);
        var cursor = from;
        foreach (var slot in slots.Where(s => s.End > from && s.Start < to).OrderBy(s => s.Start))
        {
            Assert.True(slot.Start <= cursor, $"Lücke/Überlappung bei {slot.Start:o} (erwartet <= {cursor:o})");
            cursor = slot.End;
        }
        Assert.True(cursor >= to, $"Abdeckung endet bei {cursor:o}, erwartet >= {to:o}");
    }

    public void Dispose() => _connection.Dispose();
}
