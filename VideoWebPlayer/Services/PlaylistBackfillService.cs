using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// The snapshot of a marker (<see cref="PlaylistBackfillMarker"/>) taken when a backfill run was planned: its
/// id and the <see cref="PlaylistBackfillMarker.Version"/> that was processed. The marker is removed afterwards
/// only if its version is still this one.
/// </summary>
/// <param name="Id">The marker identifier.</param>
/// <param name="Version">The marker version at snapshot time.</param>
/// <returns>A value pairing a marker id with its processed version.</returns>
internal readonly record struct PlaylistBackfillMarkerSnapshot(long Id, long Version);

/// <summary>
/// The work derived from the pending markers: which playlists have to be processed, and which markers relate
/// to which playlist (so a marker stays when one of "its" playlists failed).
/// </summary>
/// <param name="Markers">Every marker that existed when the plan was made.</param>
/// <param name="PlaylistIds">The ids of exactly those playlists containing a marked collection medium as an entry, ascending.</param>
/// <param name="MarkerIdsByPlaylist">For each playlist the ids of the (snapshot) markers it contains an entry for.</param>
/// <returns>A plan describing what a marker-driven backfill run has to process.</returns>
internal sealed record PlaylistBackfillPlan(
    IReadOnlyList<PlaylistBackfillMarkerSnapshot> Markers,
    IReadOnlyList<long> PlaylistIds,
    IReadOnlyDictionary<long, List<long>> MarkerIdsByPlaylist)
{
    /// <summary>
    /// Gets a value indicating whether nothing is marked at all.
    /// </summary>
    public bool IsEmpty => Markers.Count == 0;
}

/// <summary>
/// The outcome of backfilling one block of playlists.
/// </summary>
/// <param name="PlaylistsExamined">How many playlists were examined (whether or not anything was added).</param>
/// <param name="EntriesAdded">How many <see cref="PlaylistEntry"/> rows were added across the block.</param>
/// <param name="FailedPlaylistIds">The playlists whose backfill threw (the block goes on without them).</param>
/// <returns>A value describing what one block of playlists yielded.</returns>
internal readonly record struct PlaylistBackfillBlockResult(int PlaylistsExamined, int EntriesAdded, IReadOnlyList<long> FailedPlaylistIds);

/// <summary>
/// Implements the automatic-backfill business logic (Entwicklungsschritt 8): playlists that contain a
/// complete TV show, TV show season or movie collection as one of their entries pick up newly added children
/// (a new season, a new episode, a new movie in the collection) via the same
/// <see cref="MediaHierarchyRegistry.Handlers"/> cascade-lookup <see cref="PlaylistService.AddMediaToPlaylistAsync"/>
/// itself uses when the user manually adds such a collection; anything found that is not already in the
/// playlist and was not deliberately removed by the user before (<see cref="PlaylistEntryExclusion"/>) is
/// added the same way a manual add would add it - appended at the end in
/// <see cref="PlaylistSortMode.Manual"/> mode (via <see cref="PlaylistEntryReorderService.AssignSortOrderForNewEntriesAsync"/>,
/// shared with <see cref="PlaylistService"/> so the two never drift apart), or left unordered (<c>SortOrder
/// = null</c>) for <see cref="PlaylistSortMode.ByReleaseDate"/> playlists, where read-time sorting already
/// places it correctly by release date.
/// </summary>
/// <remarks>
/// What to look at is decided by markers (<see cref="PlaylistBackfillMarker"/>), which
/// <see cref="ApplicationDbContext"/> writes when a child is created: <see cref="PlanPendingAsync"/> turns them
/// into exactly the playlists that contain a marked collection medium, <see cref="BackfillPlaylistsAsync"/>
/// processes one block of playlists, and <see cref="ReleaseMarkersAsync"/> removes the processed markers (only
/// at the processed version, so a marker set meanwhile is never lost). The daily safety sweep instead walks
/// all playlists with a collection entry (<see cref="LoadSweepBlockAsync"/>) without looking at markers. This
/// class does not own scheduling, pauses between blocks or the "don't run while a backup is in progress" gate -
/// that is <see cref="PlaylistBackfillCoordinator"/> / <see cref="PlaylistBackfillWorker"/>.
/// </remarks>
internal sealed class PlaylistBackfillService
{
    private static readonly string[] CollectionMediaTypes =
    {
        MediaTypeValues.TVShow,
        MediaTypeValues.TVShowSeason,
        MediaTypeValues.MovieCollection
    };

    private const int DeleteChunkSize = 500;

    private readonly ApplicationDbContext _db;
    private readonly PlaylistSettings _playlistSettings;
    private readonly PlaylistEntryReorderService _reorderService;
    private readonly PlaylistGenreService _genreService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistBackfillService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="playlistSettings">Playlist configuration, used for <see cref="PlaylistSettings.MaxPlaylistItemCount"/>.</param>
    public PlaylistBackfillService(ApplicationDbContext db, IOptions<PlaylistSettings> playlistSettings)
    {
        _db = db;
        _playlistSettings = playlistSettings.Value;
        _reorderService = new PlaylistEntryReorderService(db);
        _genreService = new PlaylistGenreService(db);
    }

    /// <summary>
    /// Snapshots the pending markers and determines - with one join query against the
    /// <c>(MediaType, MediaId)</c> index of the playlist entries - exactly the playlists that contain a marked
    /// collection medium. With no markers this is a single cheap query and nothing else.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The plan (possibly empty).</returns>
    public async Task<PlaylistBackfillPlan> PlanPendingAsync(CancellationToken cancellationToken)
    {
        var markers = await _db.PlaylistBackfillMarkers.AsNoTracking()
            .Select(m => new PlaylistBackfillMarkerSnapshot(m.Id, m.Version))
            .ToListAsync(cancellationToken);
        if (markers.Count == 0)
            return new PlaylistBackfillPlan(markers, Array.Empty<long>(), new Dictionary<long, List<long>>());

        var pairs = await (
                from e in _db.PlaylistEntries.AsNoTracking()
                join m in _db.PlaylistBackfillMarkers.AsNoTracking()
                    on new { e.MediaType, e.MediaId } equals new { m.MediaType, m.MediaId }
                select new { e.PlaylistId, MarkerId = m.Id })
            .ToListAsync(cancellationToken);

        var snapshotIds = markers.Select(m => m.Id).ToHashSet();
        var markerIdsByPlaylist = new Dictionary<long, List<long>>();
        foreach (var pair in pairs.Where(p => snapshotIds.Contains(p.MarkerId)))
        {
            if (!markerIdsByPlaylist.TryGetValue(pair.PlaylistId, out var ids))
                markerIdsByPlaylist[pair.PlaylistId] = ids = new List<long>();
            ids.Add(pair.MarkerId);
        }

        // Playlists found only through a marker newer than the snapshot are processed as well (harmless, and
        // their marker is not removed by this run).
        var playlistIds = pairs.Select(p => p.PlaylistId).Distinct().OrderBy(id => id).ToList();
        return new PlaylistBackfillPlan(markers, playlistIds, markerIdsByPlaylist);
    }

    /// <summary>
    /// Removes the markers of a finished plan - except those belonging to a playlist whose backfill failed
    /// (they stay for the next attempt) and except those re-marked since the snapshot (their version
    /// changed, so the conditional delete does not match): a marker set while the run was in progress is
    /// never lost.
    /// </summary>
    /// <param name="plan">The plan whose markers were processed.</param>
    /// <param name="failedPlaylistIds">The playlists whose backfill failed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task completing when the markers are removed.</returns>
    public async Task ReleaseMarkersAsync(PlaylistBackfillPlan plan, IReadOnlyCollection<long> failedPlaylistIds, CancellationToken cancellationToken)
    {
        var blocked = new HashSet<long>();
        foreach (var failedId in failedPlaylistIds)
        {
            if (plan.MarkerIdsByPlaylist.TryGetValue(failedId, out var markerIds))
                blocked.UnionWith(markerIds);
        }

        foreach (var versionGroup in plan.Markers.Where(m => !blocked.Contains(m.Id)).GroupBy(m => m.Version))
        {
            var version = versionGroup.Key;
            foreach (var chunk in versionGroup.Select(m => m.Id).Chunk(DeleteChunkSize))
            {
                await _db.PlaylistBackfillMarkers
                    .Where(m => chunk.Contains(m.Id) && m.Version == version)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Loads the next block of the safety sweep: up to <paramref name="blockSize"/> ids, ascending, of
    /// playlists that contain at least one TVShow/TVShowSeason/MovieCollection entry, with an id greater than
    /// <paramref name="afterPlaylistId"/> (0 to start from the beginning).
    /// </summary>
    /// <param name="afterPlaylistId">Continue after this playlist id.</param>
    /// <param name="blockSize">The maximum number of playlists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playlist ids of the block (empty when the sweep is through).</returns>
    public Task<List<long>> LoadSweepBlockAsync(long afterPlaylistId, int blockSize, CancellationToken cancellationToken)
        => _db.PlaylistEntries
            .Where(e => CollectionMediaTypes.Contains(e.MediaType) && e.PlaylistId > afterPlaylistId)
            .Select(e => e.PlaylistId)
            .Distinct()
            .OrderBy(id => id)
            .Take(blockSize)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Backfills newly available cascade children into every playlist of the given block. A failure in one
    /// playlist does not stop the others: it is reported in the result, the change tracker is cleared and the
    /// next playlist is processed.
    /// </summary>
    /// <param name="playlistIds">The playlist ids of the block.</param>
    /// <param name="cancellationToken">Cancellation token, checked between playlists (cancellation is not treated as a failure).</param>
    /// <returns>How many playlists were examined, how many entries were added, and which playlists failed.</returns>
    public async Task<PlaylistBackfillBlockResult> BackfillPlaylistsAsync(IReadOnlyList<long> playlistIds, CancellationToken cancellationToken)
    {
        var totalAdded = 0;
        var failed = new List<long>();

        foreach (var playlistId in playlistIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                totalAdded += await BackfillPlaylistAsync(playlistId, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                failed.Add(playlistId);
                _db.ChangeTracker.Clear(); // Half-built entries of the failed playlist must not poison the next one.
            }
        }

        return new PlaylistBackfillBlockResult(playlistIds.Count, totalAdded, failed);
    }

    /// <summary>
    /// Backfills newly available cascade children into a single playlist's TVShow/TVShowSeason/
    /// MovieCollection entries.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many entries were added.</returns>
    private async Task<int> BackfillPlaylistAsync(long playlistId, CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);
        if (playlist is null)
            return 0; // Deleted concurrently since it was picked up as a candidate.

        var allEntryRefs = (await _db.PlaylistEntries
                .Where(e => e.PlaylistId == playlistId)
                .Select(e => new { e.MediaType, e.MediaId })
                .ToListAsync(cancellationToken))
            .Select(e => (MediaType: e.MediaType, MediaId: e.MediaId))
            .ToList();

        var collectionEntries = allEntryRefs.Where(e => CollectionMediaTypes.Contains(e.MediaType)).ToList();
        if (collectionEntries.Count == 0)
            return 0;

        var existingRefs = allEntryRefs.ToHashSet();
        var existingEntryCount = existingRefs.Count;

        var exclusions = (await _db.PlaylistEntryExclusions
                .Where(x => x.PlaylistId == playlistId)
                .Select(x => new { x.MediaType, x.MediaId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.MediaType, x.MediaId))
            .ToHashSet();

        var toAdd = await BuildEntriesToBackfillAsync(playlistId, collectionEntries, existingRefs, exclusions, cancellationToken);
        if (toAdd.Count == 0)
            return 0;

        // "Ist für die Playlist ein konfiguriertes Maximum an Titeln aktiv und erreicht, werden keine
        // weiteren Titel nachgeliefert." Interpreted as: fill up to the configured maximum (rather than
        // rejecting the whole batch the way AddMediaToPlaylistAsync does for a manual add), since a manual
        // add is a single user-facing action that can fail with an error message - a silent background run
        // has no one to show that error to, and partially filling up to the limit still satisfies "keine
        // weiteren Titel nachgeliefert" once the limit is hit. If the limit is already reached, nothing is
        // added for this playlist at all (this run - a later run added, e.g. the user raising the limit or
        // removing entries elsewhere, would allow it to gain room again).
        if (_playlistSettings.MaxPlaylistItemCount is int maxItemCount)
        {
            var room = maxItemCount - existingEntryCount;
            if (room <= 0)
                return 0;
            if (toAdd.Count > room)
                toAdd = toAdd.Take(room).ToList();
        }

        await _reorderService.AssignSortOrderForNewEntriesAsync(playlist, toAdd, cancellationToken);

        await _db.PlaylistEntries.AddRangeAsync(toAdd, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _genreService.RecomputeGenresAsync(playlist, cancellationToken);

        return toAdd.Count;
    }

    /// <summary>
    /// Builds the <see cref="PlaylistEntry"/> rows to backfill for the given collection entries of a single
    /// playlist: for each, resolves its current cascade children via the same
    /// <see cref="MediaHierarchyRegistry.Handlers"/> lookup <see cref="PlaylistService"/> uses for a manual
    /// "add complete series/season/collection", and keeps only those that are neither already present
    /// (<paramref name="existingRefs"/> - mutated in place as entries are built, so two collection entries
    /// of the same playlist that happen to share a child are never both queued) nor deliberately excluded by
    /// the user before (<paramref name="exclusions"/>).
    /// </summary>
    /// <param name="playlistId">The playlist identifier the built entries belong to.</param>
    /// <param name="collectionEntries">The playlist's TVShow/TVShowSeason/MovieCollection entries to resolve cascade children for.</param>
    /// <param name="existingRefs">The (media type, media id) references already present in the playlist; extended as entries are built.</param>
    /// <param name="exclusions">The (media type, media id) references the user deliberately removed from this playlist before.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new, not-yet-persisted <see cref="PlaylistEntry"/> rows to add.</returns>
    private async Task<List<PlaylistEntry>> BuildEntriesToBackfillAsync(
        long playlistId,
        List<(string MediaType, long MediaId)> collectionEntries,
        HashSet<(string MediaType, long MediaId)> existingRefs,
        HashSet<(string MediaType, long MediaId)> exclusions,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var toAdd = new List<PlaylistEntry>();

        foreach (var collectionEntry in collectionEntries)
        {
            if (!MediaHierarchyRegistry.TryParseKnownMediaType(collectionEntry.MediaType, out var parsedType))
                continue;

            var handler = MediaHierarchyRegistry.Handlers[parsedType];
            if (handler.LoadCascadeChildrenAsync is null)
                continue;

            var children = await handler.LoadCascadeChildrenAsync(_db, collectionEntry.MediaId, cancellationToken);
            foreach (var (childMediaType, childMediaId) in children)
            {
                var childRef = (MediaType: childMediaType.ToString(), MediaId: childMediaId);
                if (existingRefs.Contains(childRef) || exclusions.Contains(childRef))
                    continue;

                toAdd.Add(new PlaylistEntry
                {
                    PlaylistId = playlistId,
                    MediaType = childRef.MediaType,
                    MediaId = childRef.MediaId,
                    ParentMediaType = collectionEntry.MediaType,
                    ParentMediaId = collectionEntry.MediaId,
                    AddedAt = now
                });
                existingRefs.Add(childRef);
            }
        }

        return toAdd;
    }
}
