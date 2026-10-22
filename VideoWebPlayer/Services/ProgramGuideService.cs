using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements <see cref="IProgramGuideService"/>: the purely time-based, stateless program guide
/// ("Programmauskunft") over the <see cref="ProgramSlot"/> schedule. Both queries are read-only
/// and deterministic — they answer solely from the clock and the persisted schedule, so the guide
/// is correct immediately after a server restart and regardless of who is watching.
/// </summary>
public sealed class ProgramGuideService : IProgramGuideService
{
    /// <summary>
    /// The maximum look-ahead the bounded program overview accepts — the full schedule remains an
    /// administrative view.
    /// </summary>
    public const int MaxPreviewHours = 12;

    // Title shown when a slot's soft content reference no longer resolves — mirrors the
    // administration schedule view (TVSenderAdmin).
    private const string UnknownTitle = "Unbekannt";

    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">Database context.</param>
    public ProgramGuideService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<DtoProgramGuideNow> GetNowAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var result = new DtoProgramGuideNow { ServerTimeUtc = utcNow };
        if (await GetChannelIdAsync(cancellationToken) is not { } channelId)
            return result;

        var now = utcNow.UtcDateTime;
        var current = await _db.ProgramSlots
            .AsNoTracking()
            .Where(s => s.TVChannelId == channelId && s.Status == ProgramSlotStatus.Scheduled
                && s.Start <= now && s.End > now)
            .OrderBy(s => s.Start)
            .FirstOrDefaultAsync(cancellationToken);

        // The next broadcast starts at or after the current broadcast's end (gapless schedule);
        // during a schedule gap it is simply the earliest upcoming scheduled slot.
        var nextFrom = current?.End ?? now;
        var next = await _db.ProgramSlots
            .AsNoTracking()
            .Where(s => s.TVChannelId == channelId && s.Status == ProgramSlotStatus.Scheduled
                && s.Start >= nextFrom)
            .OrderBy(s => s.Start)
            .FirstOrDefaultAsync(cancellationToken);

        var details = await LoadDetailsAsync([current, next], cancellationToken);
        result.Now = current is null ? null : ToEntry(current, details, utcNow);
        result.Next = next is null ? null : ToEntry(next, details, utcNow);
        return result;
    }

    /// <inheritdoc />
    public async Task<DtoProgramGuideEntry[]> GetUpcomingAsync(DateTimeOffset utcNow, TimeSpan preview, CancellationToken cancellationToken = default)
    {
        if (preview <= TimeSpan.Zero || preview > TimeSpan.FromHours(MaxPreviewHours))
            throw new ArgumentOutOfRangeException(nameof(preview), preview,
                $"Die Programmvorschau muss zwischen 0 (ausschließlich) und {MaxPreviewHours} Stunden liegen.");

        if (await GetChannelIdAsync(cancellationToken) is not { } channelId)
            return [];

        var now = utcNow.UtcDateTime;
        var windowEnd = now + preview;
        var slots = await _db.ProgramSlots
            .AsNoTracking()
            .Where(s => s.TVChannelId == channelId && s.Status == ProgramSlotStatus.Scheduled
                && s.End > now && s.Start < windowEnd)
            .OrderBy(s => s.Start)
            .ToListAsync(cancellationToken);

        var details = await LoadDetailsAsync(slots, cancellationToken);
        return slots.Select(s => ToEntry(s, details, utcNow)).ToArray();
    }

    // The guide is a pure read service: it picks the single delivered channel directly instead of
    // calling IProgramRuleService.GetOrCreateChannelAsync, because an information request must not
    // create the channel row as a side effect.
    private async Task<long?> GetChannelIdAsync(CancellationToken cancellationToken)
        => await _db.TVChannels
            .OrderBy(c => c.Id)
            .Select(c => (long?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // Batch-resolves everything the cards need of all given slots: movies and episodes first
    // (name, plot, poster, genres, season/show info), then the bare media item name for slots
    // without a content reference (e.g. clip-source filler slots).
    private async Task<GuideDetails> LoadDetailsAsync(IEnumerable<ProgramSlot?> slots, CancellationToken cancellationToken)
    {
        var list = slots.Where(s => s is not null).Cast<ProgramSlot>().ToList();
        var movieIds = list.Where(s => s.MovieId is not null).Select(s => s.MovieId!.Value).Distinct().ToList();
        var episodeIds = list.Where(s => s.TVShowEpisodeId is not null).Select(s => s.TVShowEpisodeId!.Value).Distinct().ToList();
        var bareItemIds = list.Where(s => s.MovieId is null && s.TVShowEpisodeId is null).Select(s => s.MediaItemId).Distinct().ToList();

        var movies = movieIds.Count == 0
            ? new Dictionary<long, MovieDetails>()
            : await _db.Movies
                .Where(m => movieIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => new MovieDetails(m.Name, m.Plot, m.PosterPictureId, m.GenreNames), cancellationToken);

        var episodes = episodeIds.Count == 0
            ? new Dictionary<long, EpisodeDetails>()
            : await _db.TVShowEpisodes
                .Where(e => episodeIds.Contains(e.Id))
                .Select(e => new EpisodeDetails(
                    e.Id, e.Name, e.Number, e.Plot, e.PosterPictureId,
                    e.TVShowSeason.Name, e.TVShowSeason.PosterPictureId,
                    e.TVShowSeason.TVShow.Name, e.TVShowSeason.TVShow.Plot,
                    e.TVShowSeason.TVShow.PosterPictureId, e.TVShowSeason.TVShow.GenreNames))
                .ToDictionaryAsync(e => e.Id, cancellationToken);

        var items = bareItemIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.MediaItems
                .Where(i => bareItemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);

        return new GuideDetails(movies, episodes, items);
    }

    private static DtoProgramGuideEntry ToEntry(ProgramSlot slot, GuideDetails details, DateTimeOffset utcNow)
    {
        var entry = new DtoProgramGuideEntry
        {
            SlotId = slot.Id,
            MediaItemId = slot.MediaItemId,
            IsFiller = slot.RuleType == ProgramRuleType.Filler,
            StartUtc = new DateTimeOffset(DateTime.SpecifyKind(slot.Start, DateTimeKind.Utc)),
            EndUtc = new DateTimeOffset(DateTime.SpecifyKind(slot.End, DateTimeKind.Utc)),
            DurationSeconds = (slot.End - slot.Start).TotalSeconds,
            Title = UnknownTitle
        };

        if (slot.MovieId is { } movieId)
        {
            entry.MediaType = MediaTypeValues.Movie;
            entry.MediaId = movieId;
            if (details.Movies.TryGetValue(movieId, out var movie))
            {
                entry.Title = movie.Name;
                entry.Description = movie.Plot;
                entry.PosterPictureId = movie.PosterPictureId;
                entry.GenreName = ProgramGuideEntryText.FirstGenre(movie.GenreNames);
            }
        }
        else if (slot.TVShowEpisodeId is { } episodeId)
        {
            entry.MediaType = MediaTypeValues.TVShowEpisode;
            entry.MediaId = episodeId;
            if (details.Episodes.TryGetValue(episodeId, out var episode))
            {
                entry.Title = $"{episode.ShowName} — {episode.SeasonName}, E{episode.Number}: {episode.Name}";
                entry.ShowName = episode.ShowName;
                entry.SeasonName = episode.SeasonName;
                entry.SeasonNumber = ProgramGuideEntryText.SeasonNumberFromName(episode.SeasonName);
                entry.EpisodeNumber = episode.Number;
                entry.EpisodeName = episode.Name;
                entry.Description = episode.Plot ?? episode.ShowPlot;
                entry.PosterPictureId = episode.PosterPictureId ?? episode.SeasonPosterPictureId ?? episode.ShowPosterPictureId;
                entry.GenreName = ProgramGuideEntryText.FirstGenre(episode.ShowGenreNames);
            }
        }
        else if (details.Items.TryGetValue(slot.MediaItemId, out var itemName) && !string.IsNullOrWhiteSpace(itemName))
        {
            entry.Title = itemName;
        }

        var now = utcNow.UtcDateTime;
        if (slot.Start <= now && slot.End > now)
        {
            entry.PositionSeconds = (now - slot.Start).TotalSeconds;
            entry.RemainingSeconds = (slot.End - now).TotalSeconds;
        }

        return entry;
    }

    private sealed record MovieDetails(string Name, string? Plot, long? PosterPictureId, string? GenreNames);

    private sealed record EpisodeDetails(
        long Id, string Name, int Number, string? Plot, long? PosterPictureId,
        string SeasonName, long? SeasonPosterPictureId,
        string ShowName, string? ShowPlot, long? ShowPosterPictureId, string? ShowGenreNames);

    private sealed record GuideDetails(
        Dictionary<long, MovieDetails> Movies,
        Dictionary<long, EpisodeDetails> Episodes,
        Dictionary<long, string> Items);
}
