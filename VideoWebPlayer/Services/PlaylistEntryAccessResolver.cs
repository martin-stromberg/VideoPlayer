using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Resolves whether playlist entries are accessible to a given user, following the canonical
/// application rule <c>hasSourceAccess OR isUnlocked</c> (see <see cref="IUnlockedMediaService.IsAccessible"/>),
/// mirroring <c>ItemsController.EnsureAccessAsync</c>. Every lookup is bulk-loaded once per request
/// (scaled to the number of distinct media types among the entries, not to the number of entries) so
/// accessibility can then be checked per entry in-memory without N+1 queries.
/// </summary>
internal sealed class PlaylistEntryAccessResolver
{
    private readonly ApplicationDbContext _db;
    private readonly IUnlockedMediaService _unlockedMediaService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistEntryAccessResolver"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="unlockedMediaService">Service used to check per-entry unlock/access status.</param>
    public PlaylistEntryAccessResolver(ApplicationDbContext db, IUnlockedMediaService unlockedMediaService)
    {
        _db = db;
        _unlockedMediaService = unlockedMediaService;
    }

    /// <summary>
    /// Bulk-loads the ids of all movie collections and TV shows currently unlocked for the given user,
    /// as well as the ids of all media sources the user has regular access to (one query each,
    /// regardless of the number of playlist entries).
    /// </summary>
    /// <param name="userId">The id of the user to load access check data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bulk-loaded <see cref="AccessCheckData"/> for the user.</returns>
    public async Task<AccessCheckData> LoadAccessCheckDataAsync(string userId, CancellationToken cancellationToken)
    {
        var unlockedMovieCollectionIds = await _unlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync(userId, cancellationToken);
        var unlockedTVShowIds = await _unlockedMediaService.GetUnlockedTVShowIdsForUserAsync(userId, cancellationToken);
        var mediaSourceIds = await _unlockedMediaService.GetMediaSourceIdsForUserAsync(userId, cancellationToken);
        return new AccessCheckData(unlockedMovieCollectionIds.ToHashSet(), unlockedTVShowIds.ToHashSet(), mediaSourceIds.ToHashSet());
    }

    /// <summary>
    /// Bulk-loads, for the movie, TV show season and TV show episode ids among the given ids-by-type map,
    /// the id that should be used to check whether they are individually unlocked (the movie collection
    /// id for a movie, the TV show id for a season or episode), mirroring the hierarchy resolution in
    /// <c>ItemsController</c>.
    /// </summary>
    /// <param name="idsByType">
    /// The entries' media ids grouped by media type (see <see cref="MediaHierarchyRegistry.GroupMediaIdsByType"/>),
    /// passed in rather than recomputed here so callers that already grouped the same entries for another
    /// purpose (e.g. loading titles) do not repeat the grouping.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of media type to a dictionary of media id to the id used for the unlock check.</returns>
    public async Task<Dictionary<MediaType, Dictionary<long, long>>> LoadUnlockHierarchyMappingsAsync(Dictionary<string, HashSet<long>> idsByType, CancellationToken cancellationToken)
    {
        var hierarchyMappings = new Dictionary<MediaType, Dictionary<long, long>>();

        if (idsByType.TryGetValue(MediaType.Movie.ToString(), out var movieIds))
        {
            hierarchyMappings[MediaType.Movie] = (await _db.Movies.AsNoTracking()
                .Where(m => movieIds.Contains(m.Id) && m.MovieCollectionId != null)
                .Select(m => new { m.Id, MovieCollectionId = m.MovieCollectionId!.Value })
                .ToListAsync(cancellationToken))
                .ToDictionary(m => m.Id, m => m.MovieCollectionId);
        }

        if (idsByType.TryGetValue(MediaType.TVShowSeason.ToString(), out var seasonIds))
        {
            hierarchyMappings[MediaType.TVShowSeason] = await _db.TVShowSeasons.AsNoTracking()
                .Where(s => seasonIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.TVShowId, cancellationToken);
        }

        if (idsByType.TryGetValue(MediaType.TVShowEpisode.ToString(), out var episodeIds))
        {
            hierarchyMappings[MediaType.TVShowEpisode] = (await _db.TVShowEpisodes.AsNoTracking()
                .Where(e => episodeIds.Contains(e.Id))
                .Select(e => new { e.Id, ShowId = e.TVShowSeason.TVShowId })
                .ToListAsync(cancellationToken))
                .ToDictionary(e => e.Id, e => e.ShowId);
        }

        return hierarchyMappings;
    }

    /// <summary>
    /// Bulk-loads the <see cref="MediaBaseEntry.MediaSourceId"/> of every id among the given ids-by-type
    /// map, so regular source access can be checked per entry without N+1 queries.
    /// </summary>
    /// <param name="idsByType">
    /// The entries' media ids grouped by media type (see <see cref="MediaHierarchyRegistry.GroupMediaIdsByType"/>),
    /// passed in rather than recomputed here so callers that already grouped the same entries for another
    /// purpose (e.g. loading titles) do not repeat the grouping.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of media type to a dictionary of media id to media source id.</returns>
    public async Task<Dictionary<MediaType, Dictionary<long, long>>> LoadMediaSourceMappingsAsync(Dictionary<string, HashSet<long>> idsByType, CancellationToken cancellationToken)
    {
        var mediaSourceMappings = new Dictionary<MediaType, Dictionary<long, long>>();

        if (idsByType.TryGetValue(MediaType.Movie.ToString(), out var movieIds))
            mediaSourceMappings[MediaType.Movie] = await _db.Movies.AsNoTracking()
                .Where(m => movieIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.MediaSourceId, cancellationToken);

        if (idsByType.TryGetValue(MediaType.TVShowSeason.ToString(), out var seasonIds))
            mediaSourceMappings[MediaType.TVShowSeason] = await _db.TVShowSeasons.AsNoTracking()
                .Where(s => seasonIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.MediaSourceId, cancellationToken);

        if (idsByType.TryGetValue(MediaType.TVShowEpisode.ToString(), out var episodeIds))
            mediaSourceMappings[MediaType.TVShowEpisode] = await _db.TVShowEpisodes.AsNoTracking()
                .Where(e => episodeIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.MediaSourceId, cancellationToken);

        if (idsByType.TryGetValue(MediaType.TVShow.ToString(), out var showIds))
            mediaSourceMappings[MediaType.TVShow] = await _db.TVShows.AsNoTracking()
                .Where(t => showIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.MediaSourceId, cancellationToken);

        if (idsByType.TryGetValue(MediaType.MovieCollection.ToString(), out var collectionIds))
            mediaSourceMappings[MediaType.MovieCollection] = await _db.MovieCollections.AsNoTracking()
                .Where(c => collectionIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.MediaSourceId, cancellationToken);

        return mediaSourceMappings;
    }

    /// <summary>
    /// Resolves the id that should be used to check whether the given entry is individually unlocked:
    /// the entry's own id for a media type that is itself a direct unlock target (a TV show or movie
    /// collection, see <see cref="MediaTypeHandler.IsDirectUnlockTarget"/>), or the ancestor id resolved
    /// via the hierarchy mappings bulk-loaded by <see cref="LoadUnlockHierarchyMappingsAsync"/> otherwise.
    /// </summary>
    /// <param name="entry">The playlist entry to resolve the unlock check id for.</param>
    /// <param name="hierarchyMappings">The bulk-loaded unlock hierarchy mappings, by media type.</param>
    /// <returns>The id to use for the unlock check, or <c>null</c> if it could not be resolved.</returns>
    public long? ResolveUnlockMediaId(PlaylistEntry entry, Dictionary<MediaType, Dictionary<long, long>> hierarchyMappings)
    {
        if (!MediaHierarchyRegistry.TryParseKnownMediaType(entry.MediaType, out var parsedType))
            return null;

        var handler = MediaHierarchyRegistry.Handlers[parsedType];
        if (handler.IsDirectUnlockTarget)
            return entry.MediaId;

        return hierarchyMappings.TryGetValue(parsedType, out var mapping) && mapping.TryGetValue(entry.MediaId, out var resolvedId)
            ? resolvedId
            : null;
    }

    /// <summary>
    /// Resolves the <see cref="MediaBaseEntry.MediaSourceId"/> of the given entry from the bulk-loaded
    /// media source mappings built by <see cref="LoadMediaSourceMappingsAsync"/>.
    /// </summary>
    /// <param name="entry">The playlist entry to resolve the media source id for.</param>
    /// <param name="mediaSourceMappings">The bulk-loaded media source mappings, by media type.</param>
    /// <returns>The resolved media source id, or <c>null</c> if it could not be resolved.</returns>
    public long? ResolveMediaSourceId(PlaylistEntry entry, Dictionary<MediaType, Dictionary<long, long>> mediaSourceMappings)
    {
        if (!MediaHierarchyRegistry.TryParseKnownMediaType(entry.MediaType, out var parsedType))
            return null;

        return mediaSourceMappings.TryGetValue(parsedType, out var mapping) && mapping.TryGetValue(entry.MediaId, out var mediaSourceId)
            ? mediaSourceId
            : null;
    }

    /// <summary>
    /// Bulk-resolves whether each of the given entries is accessible to the given user, combining
    /// <see cref="LoadAccessCheckDataAsync"/>, <see cref="LoadUnlockHierarchyMappingsAsync"/>,
    /// <see cref="LoadMediaSourceMappingsAsync"/> and the per-entry resolution/check into a single call.
    /// Callers that only need the final accessibility flag per entry should prefer this over orchestrating
    /// the individual bulk-load and per-entry resolution steps themselves.
    /// </summary>
    /// <param name="entries">The playlist entries to resolve accessibility for.</param>
    /// <param name="userId">The id of the user the accessibility check is performed for.</param>
    /// <param name="idsByType">
    /// The entries' media ids grouped by media type (see <see cref="MediaHierarchyRegistry.GroupMediaIdsByType"/>),
    /// passed in rather than recomputed here so callers that already grouped the same entries for another
    /// purpose (e.g. loading titles) do not repeat the grouping.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary from entry (by reference) to whether it is accessible.</returns>
    public async Task<Dictionary<PlaylistEntry, bool>> ResolveAccessibilityAsync(
        List<PlaylistEntry> entries, string userId, Dictionary<string, HashSet<long>> idsByType, CancellationToken cancellationToken)
    {
        var accessCheckData = await LoadAccessCheckDataAsync(userId, cancellationToken);
        var hierarchyMappings = await LoadUnlockHierarchyMappingsAsync(idsByType, cancellationToken);
        var mediaSourceMappings = await LoadMediaSourceMappingsAsync(idsByType, cancellationToken);

        var accessibilityByEntry = new Dictionary<PlaylistEntry, bool>();
        foreach (var entry in entries)
        {
            var unlockedUnlockId = ResolveUnlockMediaId(entry, hierarchyMappings);
            var resolvedMediaSourceId = ResolveMediaSourceId(entry, mediaSourceMappings);
            accessibilityByEntry[entry] = CheckEntryAccessible(entry, unlockedUnlockId, resolvedMediaSourceId, accessCheckData);
        }

        return accessibilityByEntry;
    }

    /// <summary>
    /// Checks whether the given entry is accessible, following the canonical application rule
    /// <c>hasSourceAccess OR isUnlocked</c> via <see cref="IUnlockedMediaService.IsAccessible"/>: the
    /// user either has regular access to the entry's (resolved) media source, or the entry's (resolved)
    /// unlock id is individually unlocked for the user. Mirrors <c>ItemsController.EnsureAccessAsync</c>.
    /// </summary>
    /// <param name="entry">The playlist entry to check.</param>
    /// <param name="unlockedUnlockId">The id resolved by <see cref="ResolveUnlockMediaId"/> to check against the unlocked ids.</param>
    /// <param name="resolvedMediaSourceId">The media source id resolved by <see cref="ResolveMediaSourceId"/>.</param>
    /// <param name="accessCheckData">The bulk-loaded <see cref="AccessCheckData"/> for the user.</param>
    /// <returns><c>true</c> if the entry is accessible; otherwise, <c>false</c>.</returns>
    public bool CheckEntryAccessible(PlaylistEntry entry, long? unlockedUnlockId, long? resolvedMediaSourceId, AccessCheckData accessCheckData)
    {
        var hasSourceAccess = resolvedMediaSourceId.HasValue && accessCheckData.MediaSourceIds.Contains(resolvedMediaSourceId.Value);

        var isUnlocked = false;
        if (unlockedUnlockId.HasValue && MediaHierarchyRegistry.TryParseKnownMediaType(entry.MediaType, out var parsedType))
        {
            // MovieCollectionId and TVShowId are independent, both-starting-at-1 id spaces (separate
            // tables), so the resolved id must only be checked against the id space it was resolved
            // from - never against the union of both, or ids can collide across spaces.
            var handler = MediaHierarchyRegistry.Handlers[parsedType];
            isUnlocked = handler.UnlockIdSpace == UnlockIdSpace.MovieCollection
                ? accessCheckData.UnlockedMovieCollectionIds.Contains(unlockedUnlockId.Value)
                : accessCheckData.UnlockedTVShowIds.Contains(unlockedUnlockId.Value);
        }

        return _unlockedMediaService.IsAccessible(hasSourceAccess, isUnlocked);
    }
}
