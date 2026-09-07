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
    /// Returns all playlists for the given user.
    /// </summary>
    Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single playlist for the given user, or <c>null</c> if it does not exist.
    /// </summary>
    Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new playlist for the given user.
    /// </summary>
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
    Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a playlist for the given user.
    /// </summary>
    Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a media entry (with cascade logic for TVShow, TVShowSeason and MovieCollection) to a playlist.
    /// </summary>
    Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a media entry from a playlist.
    /// </summary>
    Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all entries of a playlist, silently removing orphaned entries whose referenced media no longer exists.
    /// </summary>
    Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a sorted, paginated page of entries of a playlist, silently removing orphaned entries whose
    /// referenced media no longer exists.
    /// </summary>
    Task<DtoPlaylistEntriesPagedResult> GetPlaylistEntriesPagedAsync(long playlistId, string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the manual sort order of a single entry of a playlist in <see cref="Data.PlaylistSortMode.Manual"/> mode.
    /// </summary>
    Task ReorderPlaylistEntryAsync(long playlistId, string userId, long entryId, long newSortOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically changes the manual sort order of multiple entries of a playlist in <see cref="Data.PlaylistSortMode.Manual"/> mode.
    /// </summary>
    Task<DtoPlaylistEntry[]> BatchReorderPlaylistEntriesAsync(long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the sort mode of a playlist, populating or clearing entries' <see cref="Data.PlaylistEntry.SortOrder"/> as needed.
    /// </summary>
    Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, string userId, string newSortMode, bool? confirmLossOfManualOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current maximum <see cref="Data.PlaylistEntry.SortOrder"/> across the entire playlist
    /// (not just a loaded/virtualized page of it), or <c>null</c> if the playlist has no entries with a
    /// SortOrder yet. Used by the "move to end" quick action, which must compute the true end-of-list
    /// position rather than the maximum among only the client's currently loaded entries.
    /// </summary>
    Task<long?> GetMaxSortOrderAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a single entry of a playlist in <see cref="Data.PlaylistSortMode.Manual"/> mode to the true
    /// beginning of the manual order, shifting every other entry's <see cref="Data.PlaylistEntry.SortOrder"/>
    /// up by one first. Used by the "move to beginning" quick action instead of
    /// <see cref="ReorderPlaylistEntryAsync"/> with <c>newSortOrder = 0</c>, which would only collide with
    /// (and lose the tie-break against) whichever entry already occupies position 0.
    /// </summary>
    Task<DtoPlaylistEntry> MoveEntryToBeginningAsync(long playlistId, string userId, long entryId, CancellationToken cancellationToken = default);
}
