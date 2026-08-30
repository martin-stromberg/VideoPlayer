using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Provides user-scoped watched status persistence and batched lookup.
/// </summary>
public sealed class WatchedStatusService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchedStatusService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    public WatchedStatusService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates or updates the watched timestamp for one movie or episode.
    /// </summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="movieId">The movie identifier, if a movie is watched.</param>
    /// <param name="episodeId">The episode identifier, if an episode is watched.</param>
    /// <param name="watchedAtUtc">The UTC watched timestamp.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task MarkWatchedAsync(
        string userId,
        long? movieId,
        long? episodeId,
        DateTime watchedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        if (movieId.HasValue == episodeId.HasValue)
            throw new ArgumentException("Exactly one title reference is required.");

        watchedAtUtc = DateTime.SpecifyKind(watchedAtUtc, DateTimeKind.Utc);

        var existing = await _db.WatchedEntries
            .FirstOrDefaultAsync(
                x => x.UserId == userId &&
                     x.MovieId == movieId &&
                     x.TVShowEpisodeId == episodeId,
                cancellationToken);

        WatchedEntry? addedEntry = null;
        if (existing is null)
        {
            addedEntry = new WatchedEntry
            {
                UserId = userId,
                MovieId = movieId,
                TVShowEpisodeId = episodeId,
                WatchedAt = watchedAtUtc
            };
            _db.WatchedEntries.Add(addedEntry);
        }
        else
        {
            existing.WatchedAt = watchedAtUtc;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (existing is null)
        {
            if (addedEntry is not null)
                _db.Entry(addedEntry).State = EntityState.Detached;

            var concurrent = await _db.WatchedEntries
                .FirstAsync(
                    x => x.UserId == userId &&
                         x.MovieId == movieId &&
                         x.TVShowEpisodeId == episodeId,
                    cancellationToken);
            concurrent.WatchedAt = watchedAtUtc;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Gets watched timestamps for the supplied movie and episode ids.
    /// </summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="movieIds">Movie identifiers to load.</param>
    /// <param name="episodeIds">Episode identifiers to load.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Watched timestamps grouped by media type.</returns>
    public async Task<WatchedStatusResult> GetWatchedAtAsync(
        string userId,
        IEnumerable<long>? movieIds,
        IEnumerable<long>? episodeIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return WatchedStatusResult.Empty;

        var movieIdSet = (movieIds ?? []).Where(id => id > 0).Distinct().ToArray();
        var episodeIdSet = (episodeIds ?? []).Where(id => id > 0).Distinct().ToArray();

        var movies = movieIdSet.Length == 0
            ? new Dictionary<long, DateTime>()
            : await _db.WatchedEntries
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.MovieId.HasValue && movieIdSet.Contains(x.MovieId.Value))
                .ToDictionaryAsync(x => x.MovieId!.Value, x => x.WatchedAt, cancellationToken);

        var episodes = episodeIdSet.Length == 0
            ? new Dictionary<long, DateTime>()
            : await _db.WatchedEntries
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.TVShowEpisodeId.HasValue && episodeIdSet.Contains(x.TVShowEpisodeId.Value))
                .ToDictionaryAsync(x => x.TVShowEpisodeId!.Value, x => x.WatchedAt, cancellationToken);

        return new WatchedStatusResult(movies, episodes);
    }

    /// <summary>
    /// Sets <see cref="DtoMediaEntry.WatchedAt"/> on movie and episode DTOs in one batched lookup.
    /// </summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="entries">DTOs to enrich.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task EnrichAsync(
        string userId,
        IEnumerable<DtoMediaEntry?> entries,
        CancellationToken cancellationToken = default)
    {
        var list = entries.Where(e => e is not null).Cast<DtoMediaEntry>().ToArray();
        var status = await GetWatchedAtAsync(
            userId,
            list.OfType<DtoMovie>().Select(x => x.Id),
            list.OfType<DtoTVShowEpisode>().Select(x => x.Id),
            cancellationToken);

        foreach (var entry in list)
        {
            entry.WatchedAt = entry switch
            {
                DtoMovie movie when status.MovieWatchedAt.TryGetValue(movie.Id, out var watchedAt) => watchedAt,
                DtoTVShowEpisode episode when status.EpisodeWatchedAt.TryGetValue(episode.Id, out var watchedAt) => watchedAt,
                _ => null
            };
        }
    }
}

/// <summary>
/// Contains watched timestamps grouped by concrete title type.
/// </summary>
/// <param name="MovieWatchedAt">Watched timestamps keyed by movie id.</param>
/// <param name="EpisodeWatchedAt">Watched timestamps keyed by episode id.</param>
public sealed record WatchedStatusResult(
    IReadOnlyDictionary<long, DateTime> MovieWatchedAt,
    IReadOnlyDictionary<long, DateTime> EpisodeWatchedAt)
{
    /// <summary>
    /// Gets an empty watched status result.
    /// </summary>
    public static WatchedStatusResult Empty { get; } = new(
        new Dictionary<long, DateTime>(),
        new Dictionary<long, DateTime>());
}
