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
        // Unbekannte-Freigabe-Modus explizit Unrestricted: die freigabelosen Seeds der
        // Bestandstests bleiben so weiterhin zu jeder Sendezeit planbar (der produktive
        // Default ist RestrictToNightWindow — Fenster-Tests wählen die Regelung explizit).
        db.Setups.Add(new Setup { TvSenderEnabled = true, ProgramScheduleUnknownRatingMode = ProgramScheduleUnknownRatingMode.Unrestricted });
        await db.SaveChangesAsync(ct);
        var fixture = new ProgramScheduleTestDb(connection, db);
        await fixture.SeedTrailerClipGroupAsync(ct);
        return fixture;
    }

    /// <summary>Seeds the reserved system clip group "Trailer" — the migration InsertData seed
    /// never runs under EnsureCreated, so fixtures add the row explicitly.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The existing or newly created trailer clip group.</returns>
    public async Task<ProgramClipGroup> SeedTrailerClipGroupAsync(CancellationToken ct)
    {
        var existing = await Db.ProgramClipGroups.FindAsync([ProgramClipGroup.TrailerGroupId], ct);
        if (existing is not null)
            return existing;
        var group = new ProgramClipGroup { Id = ProgramClipGroup.TrailerGroupId, Name = ProgramClipGroup.TrailerGroupName, IsSystem = true };
        Db.ProgramClipGroups.Add(group);
        await SaveDiagAsync();
        return group;
    }

    /// <summary>Creates a named clip group directly in the database (bypasses service validation).</summary>
    /// <param name="name">The group name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created clip group.</returns>
    public async Task<ProgramClipGroup> CreateClipGroupAsync(string name, CancellationToken ct)
    {
        var group = new ProgramClipGroup { Name = name };
        Db.ProgramClipGroups.Add(group);
        await SaveDiagAsync();
        return group;
    }

    /// <summary>Adds a media item to a clip group (direct join-row insert).</summary>
    /// <param name="groupId">The clip group id.</param>
    /// <param name="mediaItemId">The media item id.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task AddClipToGroupAsync(long groupId, long mediaItemId, CancellationToken ct)
    {
        Db.ProgramClipGroupItems.Add(new ProgramClipGroupItem { ProgramClipGroupId = groupId, MediaItemId = mediaItemId });
        await SaveDiagAsync();
    }

    public ProgramScheduleService CreateService(
        Microsoft.Extensions.Options.IOptions<VideoWebPlayer.Configuration.ProgramScheduleOptions>? scheduleOptions = null,
        Microsoft.Extensions.Logging.ILogger<ProgramScheduleService>? logger = null)
        => new(Db, new ProgramRuleService(Db), new ProgramSettingsService(Db, NullLogger<ProgramSettingsService>.Instance), logger ?? NullLogger<ProgramScheduleService>.Instance, scheduleOptions: scheduleOptions);

    public ProgramGuideService CreateGuideService()
        => new(Db);

    /// <summary>Creates a program slot directly in the database — the seed the guide tests need
    /// without going through schedule generation.</summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="startUtc">Slot start (UTC).</param>
    /// <param name="endUtc">Slot end (UTC).</param>
    /// <param name="mediaItemId">The media item the slot plays.</param>
    /// <param name="movieId">Optional movie the slot belongs to.</param>
    /// <param name="episodeId">Optional episode the slot belongs to.</param>
    /// <param name="ruleId">Optional producing rule.</param>
    /// <param name="ruleType">The rule type recorded on the slot.</param>
    /// <param name="status">The slot status.</param>
    /// <param name="isClipFill">Whether the slot is clip-fill.</param>
    /// <returns>The created program slot.</returns>
    public async Task<ProgramSlot> CreateSlotAsync(
        long channelId, DateTime startUtc, DateTime endUtc, long mediaItemId,
        long? movieId = null, long? episodeId = null, long? ruleId = null,
        ProgramRuleType ruleType = ProgramRuleType.MoviePrimeTime,
        ProgramSlotStatus status = ProgramSlotStatus.Scheduled,
        bool isClipFill = false)
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
            IsClipFill = isClipFill,
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
    /// <param name="sourceId">The media source id.</param>
    /// <param name="duration">The probed duration.</param>
    /// <returns>The created media item.</returns>
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
    /// <param name="name">The item name.</param>
    /// <param name="duration">The probed duration or <see langword="null"/>.</param>
    /// <param name="probedAt">When the duration was probed.</param>
    /// <param name="attempts">The probe attempt counter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created media item.</returns>
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

    /// <summary>Creates a movie linked to a media item with the given duration; optionally with
    /// a normalized minimum age (<see cref="Movie.AgeRatingMinAge"/>) for the age-window rules.
    /// <paramref name="ageRating"/> seeds a raw rating (e.g. an MPAA value) and normalizes it
    /// through <see cref="Utils.AgeRatingNormalizer"/> like the NFO import does — it wins over
    /// <paramref name="ageRatingMinAge"/>.</summary>
    /// <param name="name">The movie name.</param>
    /// <param name="sourceId">The media source id.</param>
    /// <param name="duration">The probed duration of the linked media item.</param>
    /// <param name="collectionId">Optional movie collection the movie belongs to.</param>
    /// <param name="releaseDate">Optional release date (drives air order in ordered strands).</param>
    /// <param name="ageRatingMinAge">Optional minimum age for the age-window rules.</param>
    /// <param name="ageRating">Optional raw age rating, normalized like the NFO import.</param>
    /// <returns>The created movie.</returns>
    public async Task<Movie> CreateMovieAsync(string name, long sourceId = 1, TimeSpan? duration = null, long? collectionId = null, DateTime? releaseDate = null, int? ageRatingMinAge = null, string? ageRating = null)
    {
        await EnsureSourceAsync(sourceId);
        var normalized = ageRating is null ? null : VideoWebPlayer.Utils.AgeRatingNormalizer.Normalize(ageRating);
        var movie = new Movie
        {
            Name = name,
            MediaSourceId = sourceId,
            MovieCollectionId = collectionId,
            CreatedAt = DateTime.UtcNow,
            ReleaseDate = releaseDate,
            AgeRatingRaw = ageRating,
            AgeRating = normalized?.DisplayValue,
            AgeRatingMinAge = normalized?.MinAge ?? ageRatingMinAge
        };
        Db.Movies.Add(movie);
        await SaveDiagAsync();
        var item = await CreateMediaItemAsync(sourceId, duration);
        Db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });
        await SaveDiagAsync();
        return movie;
    }

    /// <summary>Creates a show with one season and the given episodes (numbered 1..n, each with
    /// a probed media item); optionally with a normalized minimum age
    /// (<see cref="TVShow.AgeRatingMinAge"/>) the episodes inherit for the age-window rules.</summary>
    /// <param name="name">The show name.</param>
    /// <param name="episodeCount">How many episodes to create (numbered 1..n).</param>
    /// <param name="sourceId">The media source id.</param>
    /// <param name="duration">The probed duration of each episode's media item.</param>
    /// <param name="ageRatingMinAge">Optional minimum age for the age-window rules.</param>
    /// <param name="Show">Tuple element: the created show.</param>
    /// <param name="Season">Tuple element: the created season.</param>
    /// <returns>The created show, season and episodes.</returns>
    public async Task<(TVShow Show, TVShowSeason Season, List<TVShowEpisode> Episodes)> CreateShowAsync(string name, int episodeCount, long sourceId = 1, TimeSpan? duration = null, int? ageRatingMinAge = null)
    {
        await EnsureSourceAsync(sourceId);
        var show = new TVShow { Name = name, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow, AgeRatingMinAge = ageRatingMinAge };
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

    /// <summary>Creates a show with several seasons — the "3–8 Staffeln à 10–20 Episoden"
    /// stock of the practice tests: the episode count per season is scattered
    /// deterministically over [<paramref name="episodesPerSeasonMin"/>..
    /// <paramref name="episodesPerSeasonMax"/>] (no randomness), each episode's runtime
    /// jitters around <paramref name="baseDuration"/> by an index-dependent multiple of
    /// <paramref name="durationJitterSeconds"/> and every
    /// <paramref name="doubleLengthEveryNth"/>-th episode of the whole show (counted from 1;
    /// 0 disables) runs double length — near-equal runtimes with occasional double episodes.</summary>
    /// <param name="name">The show name.</param>
    /// <param name="seasonCount">How many seasons to create.</param>
    /// <param name="episodesPerSeasonMin">Minimum episodes per season.</param>
    /// <param name="episodesPerSeasonMax">Maximum episodes per season.</param>
    /// <param name="baseDuration">The base runtime each episode jitters around.</param>
    /// <param name="durationJitterSeconds">Jitter step in seconds (index-dependent ±0–2×).</param>
    /// <param name="doubleLengthEveryNth">Every n-th episode of the show runs double length (0 = none).</param>
    /// <param name="sourceId">The media source id.</param>
    /// <param name="ageRatingMinAge">Optional minimum age the episodes inherit.</param>
    /// <param name="Show">Tuple element: the created show.</param>
    /// <param name="Seasons">Tuple element: the created seasons.</param>
    /// <returns>The created show, its seasons and all episodes in air order
    /// (season, then number — matching <see cref="ProgramScheduleService"/>'s expansion).</returns>
    public async Task<(TVShow Show, List<TVShowSeason> Seasons, List<TVShowEpisode> Episodes)> CreateMultiSeasonShowAsync(
        string name, int seasonCount, int episodesPerSeasonMin, int episodesPerSeasonMax,
        TimeSpan baseDuration, int durationJitterSeconds = 30, int doubleLengthEveryNth = 0,
        long sourceId = 1, int? ageRatingMinAge = null)
    {
        await EnsureSourceAsync(sourceId);
        var show = new TVShow { Name = name, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow, AgeRatingMinAge = ageRatingMinAge };
        Db.TVShows.Add(show);
        await SaveDiagAsync();

        var seasons = new List<TVShowSeason>();
        var episodes = new List<TVShowEpisode>();
        var spread = Math.Max(1, episodesPerSeasonMax - episodesPerSeasonMin + 1);
        var globalIndex = 0;
        for (var s = 1; s <= seasonCount; s++)
        {
            var season = new TVShowSeason { Name = $"Staffel {s}", TVShowId = show.Id, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            Db.TVShowSeasons.Add(season);
            await SaveDiagAsync();
            seasons.Add(season);

            var episodeCount = episodesPerSeasonMin + (s - 1) * 3 % spread;
            for (var i = 1; i <= episodeCount; i++)
            {
                globalIndex++;
                var episode = new TVShowEpisode { Name = $"{name} S{s}E{i}", TVShowSeasonId = season.Id, Number = i, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
                Db.TVShowEpisodes.Add(episode);
                await SaveDiagAsync();
                var duration = baseDuration + TimeSpan.FromSeconds(((i - 1) % 5 - 2) * durationJitterSeconds);
                if (doubleLengthEveryNth > 0 && globalIndex % doubleLengthEveryNth == 0)
                    duration += baseDuration;
                var item = await CreateMediaItemAsync(sourceId, duration);
                Db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisodeId = episode.Id, MediaItemId = item.Id });
                episodes.Add(episode);
            }
        }
        await SaveDiagAsync();
        return (show, seasons, episodes);
    }

    /// <summary>Creates a movie collection plus <paramref name="movieCount"/> movies in
    /// ascending release-date order — the air order of the ordered
    /// <c>collection:{id}</c> strand.</summary>
    /// <param name="name">The collection name; movies are named "{name} {i}".</param>
    /// <param name="movieCount">How many movies to create.</param>
    /// <param name="duration">The probed runtime of every movie's media item.</param>
    /// <param name="releaseDateStart">Release date of the first movie (one day per film;
    /// default 2000-01-01).</param>
    /// <param name="sourceId">The media source id.</param>
    /// <returns>The created collection.</returns>
    public async Task<MovieCollection> CreateCollectionAsync(string name, int movieCount, TimeSpan duration, DateTime? releaseDateStart = null, long sourceId = 1)
    {
        await EnsureSourceAsync(sourceId);
        var collection = new MovieCollection { Name = name, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
        Db.MovieCollections.Add(collection);
        await SaveDiagAsync();
        var start = releaseDateStart ?? new DateTime(2000, 1, 1);
        for (var i = 1; i <= movieCount; i++)
            await CreateMovieAsync($"{name} {i}", sourceId, duration, collection.Id, releaseDate: start.AddDays(i - 1));
        return collection;
    }

    /// <summary>Seeds a practical library stock — several multi-season shows (3–8 seasons
    /// à 10–20 episodes with jittered runtimes and occasional double episodes), two movie
    /// collections and standalone movies — and wires two genres for rotation rules:
    /// "Serien" (all shows) and "Filme" (all movies including the collection films).</summary>
    /// <param name="sourceId">The media source id.</param>
    /// <param name="Show">Tuple element (per show): the created show.</param>
    /// <param name="Seasons">Tuple element (per show): its seasons.</param>
    /// <param name="Shows">Tuple element: the seeded shows with seasons and episodes.</param>
    /// <param name="Collections">Tuple element: the seeded movie collections.</param>
    /// <param name="Movies">Tuple element: the seeded standalone movies.</param>
    /// <returns>The seeded shows (with seasons and episodes), collections, standalone movies
    /// and genres.</returns>
    public async Task<(List<(TVShow Show, List<TVShowSeason> Seasons, List<TVShowEpisode> Episodes)> Shows,
        List<MovieCollection> Collections, List<Movie> Movies, List<Genre> Genres)> SeedRealisticLibraryAsync(long sourceId = 1)
    {
        var shows = new List<(TVShow, List<TVShowSeason>, List<TVShowEpisode>)>
        {
            await CreateMultiSeasonShowAsync("Serie Alpha", 4, 10, 14, TimeSpan.FromMinutes(45), durationJitterSeconds: 60, doubleLengthEveryNth: 9, sourceId: sourceId),
            await CreateMultiSeasonShowAsync("Serie Beta", 6, 12, 18, TimeSpan.FromMinutes(42), durationJitterSeconds: 45, doubleLengthEveryNth: 7, sourceId: sourceId),
            await CreateMultiSeasonShowAsync("Serie Gamma", 3, 10, 20, TimeSpan.FromMinutes(48), durationJitterSeconds: 30, doubleLengthEveryNth: 11, sourceId: sourceId)
        };
        var collections = new List<MovieCollection>
        {
            await CreateCollectionAsync("Reihe Eins", 8, TimeSpan.FromMinutes(95), sourceId: sourceId),
            await CreateCollectionAsync("Reihe Zwei", 6, TimeSpan.FromMinutes(105), sourceId: sourceId)
        };
        var movies = new List<Movie>();
        for (var i = 1; i <= 6; i++)
            movies.Add(await CreateMovieAsync($"Einzelfilm {i}", sourceId, TimeSpan.FromMinutes(85 + i * 5)));

        var showGenre = await CreateGenreAsync("Serien", sourceId);
        var movieGenre = await CreateGenreAsync("Filme", sourceId);
        foreach (var (show, _, _) in shows)
            await LinkShowGenreAsync(show.Id, showGenre.Id);
        var collectionMovieIds = await Db.Movies.Where(m => m.MovieCollectionId != null && m.MediaSourceId == sourceId).Select(m => m.Id).ToListAsync();
        foreach (var movieId in movies.Select(m => m.Id).Concat(collectionMovieIds))
            await LinkMovieGenreAsync(movieId, movieGenre.Id);
        return (shows, collections, movies, [showGenre, movieGenre]);
    }

    /// <summary>Creates a genre row on the given source (optionally hidden, with alternate names
    /// and a seasonal window).</summary>
    /// <param name="name">The genre name.</param>
    /// <param name="sourceId">The media source id.</param>
    /// <param name="isHidden">Whether the genre is hidden.</param>
    /// <param name="alternateNames">Optional alternate genre names.</param>
    /// <param name="startDate">Optional seasonal window start.</param>
    /// <param name="endDate">Optional seasonal window end.</param>
    /// <returns>The created genre.</returns>
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
    /// <param name="movieId">The movie id.</param>
    /// <param name="genreId">The genre id.</param>
    public async Task LinkMovieGenreAsync(long movieId, long genreId)
    {
        Db.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
        await SaveDiagAsync();
    }

    /// <summary>Links a genre to a TV show (TVShowGenre row).</summary>
    /// <param name="showId">The show id.</param>
    /// <param name="genreId">The genre id.</param>
    public async Task LinkShowGenreAsync(long showId, long genreId)
    {
        Db.TVShowGenres.Add(new TVShowGenre { TVShowId = showId, GenreId = genreId });
        await SaveDiagAsync();
    }

    /// <summary>Creates a program rule directly in the database (service-level validation bypassed).
    /// <paramref name="validity"/> seeds the validity window the same way the service persists it
    /// (mode-matching fields only).</summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="name">The rule name.</param>
    /// <param name="type">The rule type.</param>
    /// <param name="recurrence">The recurrence pattern.</param>
    /// <param name="timeOfDay">The local start time of the occurrence.</param>
    /// <param name="materialType">The material type.</param>
    /// <param name="materialId">The material id.</param>
    /// <param name="priority">The rule priority.</param>
    /// <param name="episodeCount">Optional episode count per airing.</param>
    /// <param name="slotDuration">Optional slot duration per airing.</param>
    /// <param name="dayOfWeek">Optional weekday restriction.</param>
    /// <param name="singleDate">Optional single airing date.</param>
    /// <param name="isActive">Whether the rule is active.</param>
    /// <param name="genreIds">Optional genre links.</param>
    /// <param name="mediaSourceIds">Optional source restriction.</param>
    /// <param name="clipGroupIds">Optional clip group links.</param>
    /// <param name="placeholderGrace">Optional placeholder grace.</param>
    /// <param name="validity">Optional validity window.</param>
    /// <param name="createdUtc">Optional creation timestamp (default <see cref="DateTime.UtcNow"/>)
    /// — deterministic control of the rule's age vs. the slots' materialization time.</param>
    /// <param name="updatedUtc">Optional last-change timestamp (default <see cref="DateTime.UtcNow"/>)
    /// — the freshness proof of re-activation/edit scenarios (Issue #351).</param>
    /// <returns>The created program rule.</returns>
    public async Task<ProgramRule> CreateRuleAsync(
        long channelId, string name, ProgramRuleType type, ProgramRuleRecurrence recurrence,
        TimeOnly timeOfDay, ProgramRuleMaterialType materialType, long materialId,
        int priority = 0, int? episodeCount = null, TimeSpan? slotDuration = null,
        DayOfWeek? dayOfWeek = null, DateOnly? singleDate = null, bool isActive = true,
        IEnumerable<long>? genreIds = null, IEnumerable<long>? mediaSourceIds = null,
        IEnumerable<long>? clipGroupIds = null, TimeSpan? placeholderGrace = null,
        ProgramRuleValidityInput? validity = null,
        DateTime? createdUtc = null, DateTime? updatedUtc = null)
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
            PlaceholderGrace = placeholderGrace ?? ProgramRule.DefaultPlaceholderGrace,
            CreatedAt = createdUtc ?? DateTime.UtcNow,
            UpdatedAt = updatedUtc ?? DateTime.UtcNow
        };
        // Same mode-to-field normalization the service persists with — not a local copy.
        ProgramRuleService.ApplyValidity(rule, validity);
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
        if (clipGroupIds is not null)
        {
            foreach (var groupId in clipGroupIds.Distinct())
                Db.ProgramRuleClipGroups.Add(new ProgramRuleClipGroup { ProgramRuleId = rule.Id, ProgramClipGroupId = groupId });
            await SaveDiagAsync();
        }
        return rule;
    }

    /// <summary>De-/re-activates a rule the exact same way
    /// <see cref="ProgramRuleService.SetRuleActiveAsync"/> does — sets <c>IsActive</c> AND
    /// bumps <c>UpdatedAt</c> (the re-activation freshness proof of Issue #351); the seed
    /// helper <see cref="CreateRuleAsync"/> or a direct <c>IsActive</c> write would leave the
    /// timestamp untouched.</summary>
    /// <param name="rule">The tracked rule entity.</param>
    /// <param name="isActive">The new active flag.</param>
    /// <param name="updatedUtc">Optional explicit timestamp (default
    /// <see cref="DateTime.UtcNow"/>) for deterministic comparisons against slot
    /// materialization times.</param>
    public async Task SetRuleActiveAsync(ProgramRule rule, bool isActive, DateTime? updatedUtc = null)
    {
        rule.IsActive = isActive;
        rule.UpdatedAt = updatedUtc ?? DateTime.UtcNow;
        await SaveDiagAsync();
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
    /// <param name="fixture">The test fixture.</param>
    /// <param name="summary">The reconcile summary.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="context">Optional context for the failure message.</param>
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

    /// <summary>Asserts a reconcile run was a full no-op: nothing displaced AND nothing created
    /// (<see cref="AssertNoDisplacedAsync"/> covers only the displaced side — a repeated run
    /// that silently re-materializes stays invisible there).</summary>
    /// <param name="fixture">The test fixture.</param>
    /// <param name="summary">The reconcile summary.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="context">Optional context for the failure message.</param>
    public static async Task AssertReconcileNoOpAsync(ProgramScheduleTestDb fixture, ProgramScheduleReconcileSummary summary, CancellationToken ct, string? context = null)
    {
        if (summary.SlotsDisplaced == 0 && summary.SlotsCreated == 0)
            return;
        var dumped = (await fixture.SlotsAsync(ct))
            .Select(s => $"{(s.Status == ProgramSlotStatus.Displaced ? "X" : "S")} {s.Start:dd.HH:mm}-{s.End:dd.HH:mm} rule={s.ProgramRuleId} type={s.RuleType} ep={s.TVShowEpisodeId} item={s.MediaItemId}")
            .ToList();
        Assert.Fail($"{(context is null ? "" : context + " — ")}No-Op erwartet (Displaced={summary.SlotsDisplaced}, Created={summary.SlotsCreated}): " + string.Join(" | ", dumped));
    }

    /// <summary>Asserts no displaced slot was replaced "by itself": fails when a
    /// <see cref="ProgramSlotStatus.Displaced"/> row shares its exact time interval and content
    /// (same <see cref="ProgramSlot.MovieId"/>/<see cref="ProgramSlot.TVShowEpisodeId"/>; for raw
    /// media the <see cref="ProgramSlot.MediaItemId"/>) with a
    /// <see cref="ProgramSlotStatus.Scheduled"/> row — the direct proof of an unnecessary
    /// displacement.</summary>
    /// <param name="fixture">The test fixture.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="context">Optional context for the failure message.</param>
    public static async Task AssertNoSelfDisplacedAsync(ProgramScheduleTestDb fixture, CancellationToken ct, string? context = null)
    {
        var slots = await fixture.SlotsAsync(ct);
        var scheduled = slots.Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();
        var twins = slots
            .Where(d => d.Status == ProgramSlotStatus.Displaced)
            .Where(d => scheduled.Any(s => s.Start == d.Start && s.End == d.End
                && (s.MediaItemId == d.MediaItemId
                    || (d.MovieId is not null && s.MovieId == d.MovieId)
                    || (d.TVShowEpisodeId is not null && s.TVShowEpisodeId == d.TVShowEpisodeId))))
            .Select(d => $"{d.Start:dd.HH:mm}-{d.End:dd.HH:mm} rule={d.ProgramRuleId} movie={d.MovieId} ep={d.TVShowEpisodeId} item={d.MediaItemId}")
            .ToList();
        if (twins.Count == 0)
            return;
        Assert.Fail((context is null ? "Selbst-Verdrängung: " : context + " — Selbst-Verdrängung: ") + string.Join(" | ", twins));
    }

    /// <summary>Links an additional media item to an existing movie — reproduces a
    /// representative switch when the new link changes the probe picture
    /// (e.g. a second file that gets its duration probed later).</summary>
    /// <param name="movieId">The movie id.</param>
    /// <param name="mediaItemId">The media item id.</param>
    /// <returns>The created link row.</returns>
    public async Task<MovieMediaItem> LinkMovieMediaItemAsync(long movieId, long mediaItemId)
    {
        var link = new MovieMediaItem { MovieId = movieId, MediaItemId = mediaItemId };
        Db.MovieMediaItems.Add(link);
        await SaveDiagAsync();
        return link;
    }

    /// <summary>Links an additional media item to an existing episode (same purpose as
    /// <see cref="LinkMovieMediaItemAsync"/>).</summary>
    /// <param name="episodeId">The episode id.</param>
    /// <param name="mediaItemId">The media item id.</param>
    /// <returns>The created link row.</returns>
    public async Task<TVShowEpisodeMediaItem> LinkEpisodeMediaItemAsync(long episodeId, long mediaItemId)
    {
        var link = new TVShowEpisodeMediaItem { TVShowEpisodeId = episodeId, MediaItemId = mediaItemId };
        Db.TVShowEpisodeMediaItems.Add(link);
        await SaveDiagAsync();
        return link;
    }

    /// <summary>Asserts the slot chain covers [from, to] without gaps or overlaps: the first
    /// slot in the window may straddle <paramref name="from"/> (a slot already running at the
    /// window edge), every later slot must start exactly at its predecessor's end.</summary>
    /// <param name="slots">The slots to check.</param>
    /// <param name="from">Start of the window to cover (UTC).</param>
    /// <param name="to">End of the window to cover (UTC).</param>
    public static void AssertGapless(IReadOnlyList<ProgramSlot> slots, DateTime from, DateTime to)
    {
        Assert.NotEmpty(slots);
        var cursor = from;
        var first = true;
        foreach (var slot in slots.Where(s => s.End > from && s.Start < to).OrderBy(s => s.Start))
        {
            Assert.True(slot.Start <= cursor, $"Lücke bei {slot.Start:o} — Abdeckung endete bei {cursor:o}");
            if (slot.Start < cursor && !first)
                Assert.Fail($"Überlappung: Slot {slot.Start:o}–{slot.End:o} beginnt vor Abdeckungsende {cursor:o}");
            cursor = slot.End;
            first = false;
        }
        Assert.True(cursor >= to, $"Abdeckung endet bei {cursor:o}, erwartet >= {to:o}");
    }

    public void Dispose() => _connection.Dispose();
}
