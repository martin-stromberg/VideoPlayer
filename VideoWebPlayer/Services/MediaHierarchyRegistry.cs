using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Identifies which id space (movie collections vs. TV shows) a media type's unlock check resolves to.
/// Mirrors the two independent, both-starting-at-1 id spaces of <c>UnlockedMediaEntries</c>.
/// </summary>
internal enum UnlockIdSpace
{
    MovieCollection,
    TVShow
}

/// <summary>
/// Bulk-loading strategy for one <see cref="MediaType"/>, used by <see cref="PlaylistService"/> and
/// <see cref="PlaylistEntryAccessResolver"/> to resolve titles, cascade children, release dates,
/// hierarchy sequence numbers, picture ids and unlock characteristics without per-entry (N+1) queries.
/// </summary>
internal sealed class MediaTypeHandler
{
    public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, string>>> LoadTitlesAsync { get; init; }

    /// <summary>
    /// Checks which of the given ids still reference existing media, without fetching their titles.
    /// Used for orphan detection over an entire playlist, where resolving titles would be wasted work
    /// for every entry that is not part of the page actually being returned to the caller.
    /// </summary>
    public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<HashSet<long>>> LoadExistingIdsAsync { get; init; }

    public Func<ApplicationDbContext, long, CancellationToken, Task<List<(MediaType MediaType, long MediaId)>>>? LoadCascadeChildrenAsync { get; init; }

    public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, DateTime?>>> LoadReleaseDateAsync { get; init; }

    public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, (long? ParentId, int? SequenceNumber)>>> GetHierarchySequenceAsync { get; init; }

    /// <summary>
    /// Bulk-loads the resolved picture id (poster, falling back to banner and then fanart) for each of
    /// the given ids in a single query, matching the fallback convention used by <c>MediaBaseEntryList.razor</c>.
    /// </summary>
    public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, long?>>> LoadPictureIdsAsync { get; init; }

    /// <summary>
    /// Whether an entry of this media type is itself the entity whose individual unlock status is
    /// checked (a TV show or movie collection), as opposed to needing hierarchy resolution up to such
    /// an ancestor (a movie, TV show season or TV show episode).
    /// </summary>
    public required bool IsDirectUnlockTarget { get; init; }

    /// <summary>
    /// The id space (movie collection ids vs. TV show ids) that this media type's resolved unlock id
    /// must be checked against.
    /// </summary>
    public required UnlockIdSpace UnlockIdSpace { get; init; }
}

/// <summary>
/// Registry of <see cref="MediaTypeHandler"/> strategies for every <see cref="MediaType"/> that can be
/// referenced by a playlist entry, replacing scattered <c>MediaType</c> type checks with per-type
/// configuration.
/// </summary>
internal static class MediaHierarchyRegistry
{
    public static readonly IReadOnlyDictionary<MediaType, MediaTypeHandler> Handlers = new Dictionary<MediaType, MediaTypeHandler>
    {
        [MediaType.Movie] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.Movies.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.Movies.AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToHashSetAsync(ct),
            LoadReleaseDateAsync = (db, ids, ct) => db.Movies.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.ReleaseDate ?? m.PremieredAt, ct),
            GetHierarchySequenceAsync = NoHierarchyAsync,
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.Movies.AsNoTracking()
                    .Where(m => ids.Contains(m.Id))
                    .Select(m => new { m.Id, m.PosterPictureId, m.BannerPictureId, m.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(m => m.Id, m => ResolvePictureId(m.PosterPictureId, m.BannerPictureId, m.FanartPictureId));
            },
            IsDirectUnlockTarget = false,
            UnlockIdSpace = UnlockIdSpace.MovieCollection
        },
        [MediaType.TVShowEpisode] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShowEpisodes.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.TVShowEpisodes.AsNoTracking().Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToHashSetAsync(ct),
            LoadReleaseDateAsync = async (db, ids, ct) =>
            {
                var episodes = await db.TVShowEpisodes.AsNoTracking()
                    .Where(e => ids.Contains(e.Id))
                    .Select(e => new { e.Id, e.ReleaseDate, e.PremieredAt, ShowPremieredAt = e.TVShowSeason.TVShow.PremieredAt })
                    .ToListAsync(ct);
                return episodes.ToDictionary(e => e.Id, e => e.ReleaseDate ?? e.PremieredAt ?? e.ShowPremieredAt);
            },
            GetHierarchySequenceAsync = async (db, ids, ct) =>
            {
                var episodes = await db.TVShowEpisodes.AsNoTracking()
                    .Where(e => ids.Contains(e.Id))
                    .Select(e => new { e.Id, e.TVShowSeasonId, e.Number })
                    .ToListAsync(ct);
                return episodes.ToDictionary(e => e.Id, e => ((long?)e.TVShowSeasonId, (int?)e.Number));
            },
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.TVShowEpisodes.AsNoTracking()
                    .Where(e => ids.Contains(e.Id))
                    .Select(e => new { e.Id, e.PosterPictureId, e.BannerPictureId, e.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(e => e.Id, e => ResolvePictureId(e.PosterPictureId, e.BannerPictureId, e.FanartPictureId));
            },
            IsDirectUnlockTarget = false,
            UnlockIdSpace = UnlockIdSpace.TVShow
        },
        [MediaType.TVShowSeason] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShowSeasons.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.TVShowSeasons.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToHashSetAsync(ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var episodeIds = await db.TVShowEpisodes.AsNoTracking().Where(e => e.TVShowSeasonId == id).Select(e => e.Id).ToListAsync(ct);
                return episodeIds.Select(episodeId => (MediaType.TVShowEpisode, episodeId)).ToList();
            },
            LoadReleaseDateAsync = async (db, ids, ct) =>
            {
                var seasons = await db.TVShowSeasons.AsNoTracking()
                    .Where(s => ids.Contains(s.Id))
                    .Select(s => new
                    {
                        s.Id,
                        s.PremieredAt,
                        FirstEpisodeDate = s.Episodes.OrderBy(e => e.Number).Select(e => (DateTime?)e.ReleaseDate).FirstOrDefault(),
                        ShowPremieredAt = s.TVShow.PremieredAt
                    })
                    .ToListAsync(ct);
                return seasons.ToDictionary(s => s.Id, s => s.PremieredAt ?? s.FirstEpisodeDate ?? s.ShowPremieredAt);
            },
            GetHierarchySequenceAsync = async (db, ids, ct) =>
            {
                var seasons = await db.TVShowSeasons.AsNoTracking()
                    .Where(s => ids.Contains(s.Id))
                    .Select(s => new { s.Id, s.TVShowId })
                    .ToListAsync(ct);
                return seasons
                    .GroupBy(s => s.TVShowId)
                    .SelectMany(g => g.OrderBy(s => s.Id).Select((s, index) => (s.Id, ParentId: (long?)g.Key, SequenceNumber: (int?)(index + 1))))
                    .ToDictionary(x => x.Id, x => (x.ParentId, x.SequenceNumber));
            },
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.TVShowSeasons.AsNoTracking()
                    .Where(s => ids.Contains(s.Id))
                    .Select(s => new { s.Id, s.PosterPictureId, s.BannerPictureId, s.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(s => s.Id, s => ResolvePictureId(s.PosterPictureId, s.BannerPictureId, s.FanartPictureId));
            },
            IsDirectUnlockTarget = false,
            UnlockIdSpace = UnlockIdSpace.TVShow
        },
        [MediaType.TVShow] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShows.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.TVShows.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => t.Id).ToHashSetAsync(ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var seasonIds = await db.TVShowSeasons.AsNoTracking().Where(s => s.TVShowId == id).Select(s => s.Id).ToListAsync(ct);
                var episodeIds = await db.TVShowEpisodes.AsNoTracking().Where(e => seasonIds.Contains(e.TVShowSeasonId)).Select(e => e.Id).ToListAsync(ct);

                var result = new List<(MediaType MediaType, long MediaId)>();
                result.AddRange(seasonIds.Select(seasonId => (MediaType.TVShowSeason, seasonId)));
                result.AddRange(episodeIds.Select(episodeId => (MediaType.TVShowEpisode, episodeId)));
                return result;
            },
            LoadReleaseDateAsync = async (db, ids, ct) =>
            {
                var shows = await db.TVShows.AsNoTracking()
                    .Where(t => ids.Contains(t.Id))
                    .Select(t => new
                    {
                        t.Id,
                        t.PremieredAt,
                        FirstEpisodeDate = t.Seasons
                            .OrderBy(s => s.Id)
                            .Select(s => s.Episodes.OrderBy(e => e.Number).Select(e => (DateTime?)e.ReleaseDate).FirstOrDefault())
                            .FirstOrDefault()
                    })
                    .ToListAsync(ct);
                return shows.ToDictionary(t => t.Id, t => t.PremieredAt ?? t.FirstEpisodeDate);
            },
            GetHierarchySequenceAsync = NoHierarchyAsync,
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.TVShows.AsNoTracking()
                    .Where(t => ids.Contains(t.Id))
                    .Select(t => new { t.Id, t.PosterPictureId, t.BannerPictureId, t.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(t => t.Id, t => ResolvePictureId(t.PosterPictureId, t.BannerPictureId, t.FanartPictureId));
            },
            IsDirectUnlockTarget = true,
            UnlockIdSpace = UnlockIdSpace.TVShow
        },
        [MediaType.MovieCollection] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.MovieCollections.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.MovieCollections.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => c.Id).ToHashSetAsync(ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var movieIds = await db.Movies.AsNoTracking().Where(m => m.MovieCollectionId == id).Select(m => m.Id).ToListAsync(ct);
                return movieIds.Select(movieId => (MediaType.Movie, movieId)).ToList();
            },
            LoadReleaseDateAsync = (db, ids, ct) => db.MovieCollections.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.ReleaseDate ?? c.PremieredAt, ct),
            GetHierarchySequenceAsync = NoHierarchyAsync,
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.MovieCollections.AsNoTracking()
                    .Where(c => ids.Contains(c.Id))
                    .Select(c => new { c.Id, c.PosterPictureId, c.BannerPictureId, c.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(c => c.Id, c => ResolvePictureId(c.PosterPictureId, c.BannerPictureId, c.FanartPictureId));
            },
            IsDirectUnlockTarget = true,
            UnlockIdSpace = UnlockIdSpace.MovieCollection
        }
    };

    /// <summary>
    /// Parses and validates a raw media type string (one of the values in <c>MediaTypeValues</c>)
    /// into the internal <see cref="MediaType"/> enum.
    /// </summary>
    /// <param name="mediaType">The raw media type string to parse.</param>
    /// <returns>The parsed <see cref="MediaType"/> value.</returns>
    public static MediaType ParseMediaType(string mediaType)
    {
        if (!TryParseKnownMediaType(mediaType, out var parsed))
            throw new InvalidOperationException("Ungueltiger Medientyp.");

        return parsed;
    }

    public static bool TryParseKnownMediaType(string mediaType, out MediaType parsed)
    {
        return Enum.TryParse(mediaType, ignoreCase: true, out parsed) && Handlers.ContainsKey(parsed);
    }

    /// <summary>
    /// Groups the media ids of the given playlist entries by their (string) media type.
    /// </summary>
    /// <param name="entries">The playlist entries to group.</param>
    /// <returns>A dictionary of media type to the set of media ids of that type.</returns>
    public static Dictionary<string, HashSet<long>> GroupMediaIdsByType(IEnumerable<PlaylistEntry> entries)
    {
        var idsByType = new Dictionary<string, HashSet<long>>();
        foreach (var entry in entries)
            AddMediaId(idsByType, entry.MediaType, entry.MediaId);
        return idsByType;
    }

    /// <summary>
    /// Adds a single media id to the given media-type-to-ids map, creating the id set for that media
    /// type if it does not exist yet.
    /// </summary>
    /// <param name="map">The media-type-to-ids map to add to.</param>
    /// <param name="mediaType">The (string) media type of the id.</param>
    /// <param name="mediaId">The media id to add.</param>
    public static void AddMediaId(Dictionary<string, HashSet<long>> map, string mediaType, long mediaId)
    {
        if (!map.TryGetValue(mediaType, out var ids))
            map[mediaType] = ids = new HashSet<long>();
        ids.Add(mediaId);
    }

    private static Task<Dictionary<long, (long? ParentId, int? SequenceNumber)>> NoHierarchyAsync(ApplicationDbContext db, IReadOnlyCollection<long> ids, CancellationToken cancellationToken)
        => Task.FromResult(ids.ToDictionary(id => id, id => ((long?)null, (int?)null)));

    /// <summary>
    /// Resolves the picture id to display for a media entry, preferring the poster and falling back to
    /// banner and then fanart. Matches the fallback convention used by <c>MediaBaseEntryList.razor</c>.
    /// </summary>
    /// <param name="posterPictureId">The poster picture id, if any.</param>
    /// <param name="bannerPictureId">The banner picture id, used when no poster is available.</param>
    /// <param name="fanartPictureId">The fanart picture id, used when neither poster nor banner is available.</param>
    /// <returns>The first non-null picture id in poster, banner, fanart order; <see langword="null"/> if none are set.</returns>
    private static long? ResolvePictureId(long? posterPictureId, long? bannerPictureId, long? fanartPictureId)
        => posterPictureId ?? bannerPictureId ?? fanartPictureId;
}
