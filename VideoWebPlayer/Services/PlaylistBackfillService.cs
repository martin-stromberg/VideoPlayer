using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// The outcome of one <see cref="PlaylistBackfillService.RunBatchAsync"/> run.
/// </summary>
/// <param name="LastProcessedPlaylistId">
/// The highest playlist id examined in this run - the cursor <see cref="PlaylistBackfillWorker"/> resumes
/// from on the next run, so successive runs sweep round-robin through every eligible playlist instead of
/// repeatedly re-checking only the first ones.
/// </param>
/// <param name="PlaylistsExamined">How many playlists were examined (whether or not anything was added).</param>
/// <param name="EntriesAdded">How many <see cref="PlaylistEntry"/> rows were added across all examined playlists.</param>
/// <returns>A value combining the round-robin cursor position with how much work this run did.</returns>
public readonly record struct PlaylistBackfillRunResult(long LastProcessedPlaylistId, int PlaylistsExamined, int EntriesAdded);

/// <summary>
/// Implements the automatic-backfill business logic (Entwicklungsschritt 8): playlists that contain a
/// complete TV show, TV show season or movie collection as one of their entries are periodically checked
/// for newly added children (a new season, a new episode, a new movie in the collection) via the same
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
/// Driven entirely by <see cref="PlaylistBackfillWorker"/>, which calls <see cref="RunBatchAsync"/>
/// periodically with a bounded <c>playlistBatchSize</c> so a single run only examines a handful of
/// playlists (see <see cref="PlaylistSettings.BackfillBatchSize"/>) instead of the entire table, keeping
/// each run short enough not to noticeably affect ongoing operation as required. This class itself is
/// side-effect-scoped to the given batch and does not own scheduling, cancellation-friendliness across
/// playlists (each playlist is one bounded unit of work; the caller's <see cref="CancellationToken"/> is
/// checked between playlists), or the "don't run while a backup is in progress" gate - those are
/// <see cref="PlaylistBackfillWorker"/>'s responsibility, mirroring how <c>MediaSourceScanService</c> and
/// <c>ActorBackfillWorker</c> divide those concerns.
/// </remarks>
internal sealed class PlaylistBackfillService
{
    private static readonly string[] CollectionMediaTypes =
    {
        MediaTypeValues.TVShow,
        MediaTypeValues.TVShowSeason,
        MediaTypeValues.MovieCollection
    };

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
    /// Examines up to <paramref name="playlistBatchSize"/> playlists (that contain at least one TVShow/
    /// TVShowSeason/MovieCollection entry) with an id greater than <paramref name="afterPlaylistId"/>, and
    /// backfills any newly available cascade children into each. If no such playlist exists above
    /// <paramref name="afterPlaylistId"/> (the round-robin sweep reached the end), wraps around and takes
    /// the batch from the beginning instead, so a single call never silently does nothing just because the
    /// cursor ran past the last eligible playlist id.
    /// </summary>
    /// <param name="afterPlaylistId">
    /// Resume the round-robin sweep after this playlist id (0 to start from the beginning).
    /// </param>
    /// <param name="playlistBatchSize">The maximum number of playlists to examine in this run.</param>
    /// <param name="cancellationToken">Cancellation token, checked between playlists.</param>
    /// <returns>How much of the batch was examined, and how many entries were added.</returns>
    public async Task<PlaylistBackfillRunResult> RunBatchAsync(long afterPlaylistId, int playlistBatchSize, CancellationToken cancellationToken)
    {
        var candidatePlaylistIds = await LoadCandidatePlaylistIdsAsync(afterPlaylistId, playlistBatchSize, cancellationToken);
        if (candidatePlaylistIds.Count == 0 && afterPlaylistId > 0)
            candidatePlaylistIds = await LoadCandidatePlaylistIdsAsync(0, playlistBatchSize, cancellationToken);

        var totalAdded = 0;
        var lastProcessedId = afterPlaylistId;

        foreach (var playlistId in candidatePlaylistIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalAdded += await BackfillPlaylistAsync(playlistId, cancellationToken);
            lastProcessedId = playlistId;
        }

        return new PlaylistBackfillRunResult(lastProcessedId, candidatePlaylistIds.Count, totalAdded);
    }

    private Task<List<long>> LoadCandidatePlaylistIdsAsync(long afterPlaylistId, int playlistBatchSize, CancellationToken cancellationToken)
        => _db.PlaylistEntries
            .Where(e => CollectionMediaTypes.Contains(e.MediaType) && e.PlaylistId > afterPlaylistId)
            .Select(e => e.PlaylistId)
            .Distinct()
            .OrderBy(id => id)
            .Take(playlistBatchSize)
            .ToListAsync(cancellationToken);

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
