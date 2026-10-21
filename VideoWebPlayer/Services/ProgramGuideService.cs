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

        var titles = await LoadTitlesAsync([current, next], cancellationToken);
        result.Now = current is null ? null : ToEntry(current, titles, utcNow);
        result.Next = next is null ? null : ToEntry(next, titles, utcNow);
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

        var titles = await LoadTitlesAsync(slots, cancellationToken);
        return slots.Select(s => ToEntry(s, titles, utcNow)).ToArray();
    }

    // The guide is a pure read service: it picks the single delivered channel directly instead of
    // calling IProgramRuleService.GetOrCreateChannelAsync, because an information request must not
    // create the channel row as a side effect.
    private async Task<long?> GetChannelIdAsync(CancellationToken cancellationToken)
        => await _db.TVChannels
            .OrderBy(c => c.Id)
            .Select(c => (long?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // Batch-resolves the titles of all given slots in two queries (movies + episodes), the same
    // lookup the administration schedule view performs inline.
    private async Task<TitleLookup> LoadTitlesAsync(IEnumerable<ProgramSlot?> slots, CancellationToken cancellationToken)
    {
        var list = slots.Where(s => s is not null).Cast<ProgramSlot>().ToList();
        var movieIds = list.Where(s => s.MovieId is not null).Select(s => s.MovieId!.Value).Distinct().ToList();
        var episodeIds = list.Where(s => s.TVShowEpisodeId is not null).Select(s => s.TVShowEpisodeId!.Value).Distinct().ToList();

        var movies = movieIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.Movies
                .Where(m => movieIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        var episodes = episodeIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.TVShowEpisodes
                .Where(e => episodeIds.Contains(e.Id))
                .Select(e => new { e.Id, EpisodeName = e.Name, e.Number, SeasonName = e.TVShowSeason.Name, ShowName = e.TVShowSeason.TVShow.Name })
                .ToDictionaryAsync(e => e.Id, e => $"{e.ShowName} — {e.SeasonName}, E{e.Number}: {e.EpisodeName}", cancellationToken);

        return new TitleLookup(movies, episodes);
    }

    private static DtoProgramGuideEntry ToEntry(ProgramSlot slot, TitleLookup titles, DateTimeOffset utcNow)
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
            if (titles.Movies.TryGetValue(movieId, out var movieTitle))
                entry.Title = movieTitle;
        }
        else if (slot.TVShowEpisodeId is { } episodeId)
        {
            entry.MediaType = MediaTypeValues.TVShowEpisode;
            entry.MediaId = episodeId;
            if (titles.Episodes.TryGetValue(episodeId, out var episodeTitle))
                entry.Title = episodeTitle;
        }

        var now = utcNow.UtcDateTime;
        if (slot.Start <= now && slot.End > now)
        {
            entry.PositionSeconds = (now - slot.Start).TotalSeconds;
            entry.RemainingSeconds = (slot.End - now).TotalSeconds;
        }

        return entry;
    }

    private sealed record TitleLookup(Dictionary<long, string> Movies, Dictionary<long, string> Episodes);
}
