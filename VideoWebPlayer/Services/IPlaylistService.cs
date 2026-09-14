using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Services;

/// <summary>
/// Provides CRUD operations for managing playlists for a specific user.
/// </summary>
public interface IPlaylistService
{
    /// <summary>
    /// Returns all playlists for the given user, optionally restricted to those carrying the given genre
    /// (matching against every genre the playlist has - derived or manually assigned - not just the
    /// capped, frequency-sorted subset actually displayed).
    /// </summary>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="genreId">Optional genre id to restrict the result to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's playlists.</returns>
    Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, long? genreId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single playlist for the given user, or <c>null</c> if it does not exist.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playlist, or <c>null</c> if it does not exist.</returns>
    Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new playlist for the given user.
    /// </summary>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="name">The name of the new playlist.</param>
    /// <param name="description">The description of the new playlist, if any.</param>
    /// <param name="sortMode">The sort mode of the new playlist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created playlist.</returns>
    Task<DtoPlaylist> CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the name and description of an existing playlist for the given user.
    /// </summary>
    /// <remarks>
    /// <paramref name="sortMode"/> is accepted for signature symmetry with <see cref="CreatePlaylistAsync"/>
    /// but is intentionally ignored by <see cref="PlaylistService"/>'s implementation: changing the sort
    /// mode has data-affecting side effects (see <see cref="ChangeSortModeAsync"/>) that a plain
    /// name/description edit must not trigger. Use <see cref="ChangeSortModeAsync"/> to change the sort mode.
    /// </remarks>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="name">The new name of the playlist.</param>
    /// <param name="description">The new description of the playlist, if any.</param>
    /// <param name="sortMode">Ignored, see remarks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated playlist.</returns>
    Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a playlist for the given user.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a media entry (with cascade logic for TVShow, TVShowSeason and MovieCollection) to a playlist.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="mediaType">The type of the media entry to add.</param>
    /// <param name="mediaId">The id of the media entry to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the add operation.</returns>
    Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a media entry from a playlist. If a <see cref="Data.ContinueWatchingEntry"/> bound to this
    /// same playlist still references the entry being removed, the caller must confirm via
    /// <paramref name="confirmContinueWatchingRemoval"/> (otherwise a
    /// <see cref="ContinueWatchingConfirmationRequiredException"/> is thrown, without removing anything);
    /// once confirmed, the affected continue-watching entry is replaced with the next playable and
    /// accessible title of this playlist (after the removed entry's position), or removed entirely if no
    /// such title exists.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="mediaType">The type of the media entry to remove.</param>
    /// <param name="mediaId">The id of the media entry to remove.</param>
    /// <param name="confirmContinueWatchingRemoval">
    /// Whether the user has confirmed removal despite an existing continue-watching reference to this
    /// playlist entry. Ignored (no confirmation required) when no such reference exists.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, bool confirmContinueWatchingRemoval = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all entries of a playlist, silently removing orphaned entries whose referenced media no longer exists.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playlist's entries.</returns>
    Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a sorted, paginated page of entries of a playlist, silently removing orphaned entries whose
    /// referenced media no longer exists.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requested page of playlist entries.</returns>
    Task<DtoPlaylistEntriesPagedResult> GetPlaylistEntriesPagedAsync(long playlistId, string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the manual sort order of a single entry of a playlist in <see cref="Data.PlaylistSortMode.Manual"/> mode.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    /// <param name="newSortOrder">The new sort order of the entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReorderPlaylistEntryAsync(long playlistId, string userId, long entryId, long newSortOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically changes the manual sort order of multiple entries of a playlist in <see cref="Data.PlaylistSortMode.Manual"/> mode.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="reorderOperations">The entry ids together with their new sort orders.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reordered playlist entries.</returns>
    Task<DtoPlaylistEntry[]> BatchReorderPlaylistEntriesAsync(long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the sort mode of a playlist, populating or clearing entries' <see cref="Data.PlaylistEntry.SortOrder"/> as needed.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="newSortMode">The new sort mode of the playlist.</param>
    /// <param name="confirmLossOfManualOrder">Whether the loss of the existing manual order was confirmed by the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated playlist.</returns>
    Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, string userId, string newSortMode, bool? confirmLossOfManualOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually overrides a playlist's genres with exactly <paramref name="genreIds"/> (unknown ids are
    /// silently ignored), so they no longer follow the automatically derived genres of the playlist's
    /// contents until <see cref="ResetPlaylistGenresAsync"/> is called.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="genreIds">The genre ids to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated playlist.</returns>
    Task<DtoPlaylist> SetPlaylistGenresAsync(long playlistId, string userId, long[] genreIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a previous manual genre override (if any) and immediately recomputes the playlist's genres
    /// from its current contents.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated playlist.</returns>
    Task<DtoPlaylist> ResetPlaylistGenresAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current maximum <see cref="Data.PlaylistEntry.SortOrder"/> across the entire playlist
    /// (not just a loaded/virtualized page of it), or <c>null</c> if the playlist has no entries with a
    /// SortOrder yet. Used by the "move to end" quick action, which must compute the true end-of-list
    /// position rather than the maximum among only the client's currently loaded entries.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current maximum manual sort order, or <c>null</c> if the playlist has no entries with a sort order yet.</returns>
    Task<long?> GetMaxSortOrderAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a single entry of a playlist in <see cref="Data.PlaylistSortMode.Manual"/> mode to the true
    /// beginning of the manual order, shifting every other entry's <see cref="Data.PlaylistEntry.SortOrder"/>
    /// up by one first. Used by the "move to beginning" quick action instead of
    /// <see cref="ReorderPlaylistEntryAsync"/> with <c>newSortOrder = 0</c>, which would only collide with
    /// (and lose the tie-break against) whichever entry already occupies position 0.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The moved playlist entry.</returns>
    Task<DtoPlaylistEntry> MoveEntryToBeginningAsync(long playlistId, string userId, long entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a single entry of a playlist in <see cref="Data.PlaylistSortMode.Manual"/> mode to an
    /// arbitrary target position, shifting every other entry's <see cref="Data.PlaylistEntry.SortOrder"/>
    /// between the entry's current and target position by one first. Used by drag & drop reordering.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    /// <param name="targetSortOrder">The target sort order of the entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MoveEntryBetweenAsync(long playlistId, string userId, long entryId, long targetSortOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the next playable (Movie or TVShowEpisode) and accessible entry after
    /// <paramref name="currentEntryId"/> in the playlist's current sort order, skipping non-playable
    /// collection entries (TVShow, TVShowSeason, MovieCollection) and entries the user cannot access,
    /// together with its actual 1-based position in that sort order (accounting for any skipped entries).
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="currentEntryId">The id of the playlist entry currently playing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next playable and accessible entry with its position, or <c>null</c> if the end of the playlist is reached.</returns>
    Task<DtoPlaylistNavigationResult?> GetNextPlaylistEntryAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the previous playable and accessible entry before <paramref name="currentEntryId"/> in the
    /// playlist's current sort order, mirroring <see cref="GetNextPlaylistEntryAsync"/>.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="currentEntryId">The id of the playlist entry currently playing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The previous playable and accessible entry with its position, or <c>null</c> if the beginning of the playlist is reached.</returns>
    Task<DtoPlaylistNavigationResult?> GetPreviousPlaylistEntryAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts playback of a playlist: validates ownership and (if given) that <paramref name="entryId"/>
    /// belongs to the playlist and is accessible, or - if <paramref name="entryId"/> is <c>null</c> -
    /// resolves the first playable and accessible entry.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="entryId">The entry to start playback at, or <c>null</c> to start at the first playable and accessible entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playback start information (playlist name, position, total count and the resolved entry's stream information).</returns>
    Task<DtoPlaylistPlaybackStart> StartPlaylistAsync(long playlistId, string userId, long? entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances playback of a playlist from <paramref name="currentEntryId"/> to the next entry, for use
    /// when a title finishes playing automatically. Delegates to <see cref="GetNextPlaylistEntryAsync"/>.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="currentEntryId">The id of the playlist entry that just finished playing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next playable and accessible entry with its position, or <c>null</c> if the end of the playlist is reached.</returns>
    Task<DtoPlaylistNavigationResult?> AdvancePlaylistAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves playlist-bound continue-watching replacements for every title about to disappear together
    /// with the media source identified by <paramref name="mediaSourceId"/> - the "silent" (not
    /// user-triggered) counterpart of <see cref="RemoveMediaFromPlaylistAsync"/>'s confirmed-removal
    /// replace-with-next-available-title-or-remove behavior, for the one place in the application where a
    /// movie or TV show episode is actually deleted from the media library:
    /// <c>ApplicationDbContext.DeleteMediaSourceAsync</c>.
    /// </summary>
    /// <remarks>
    /// Must be invoked by the caller <b>before</b> <c>DeleteMediaSourceAsync</c> unconditionally deletes the
    /// affected <see cref="Data.ContinueWatchingEntry"/> rows (and ideally within the very same database
    /// transaction as the rest of that deletion, so a rollback of the source deletion also rolls back any
    /// replacement made here) - after that point there is no longer anything left to resolve, since the row
    /// this method would have replaced is already gone. The "next available title" search excludes every
    /// title that itself belongs to <paramref name="mediaSourceId"/>, since all of them disappear together
    /// with the source; a title from a different, still-existing media source mixed into the same playlist
    /// remains eligible.
    /// </remarks>
    /// <param name="mediaSourceId">The id of the media source about to be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(long mediaSourceId, CancellationToken cancellationToken = default);
}
