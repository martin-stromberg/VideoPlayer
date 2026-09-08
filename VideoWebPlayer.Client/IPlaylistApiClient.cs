using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Client
{
    /// <summary>
    /// Client-side operations for the playlist API (<c>api/playlists</c>). Narrower than the full
    /// <see cref="VideoWebPlayerClient"/> facade, so Razor components that only work with playlists
    /// (<c>PlaylistDetail</c>, <c>PlaylistsList</c>, <c>PlaylistForm</c>) can depend on just this surface.
    /// Implemented by <see cref="VideoWebPlayerClient"/> itself (see the
    /// <c>VideoWebPlayerClient.Playlists.cs</c> partial file), so it shares the same HTTP client,
    /// reauthorization and impersonation behavior as the rest of the facade.
    /// </summary>
    public interface IPlaylistApiClient
    {
        /// <summary>
        /// Requests all playlists of the current user.
        /// </summary>
        Task<IEnumerable<DtoPlaylist>> RequestPlaylistsAsync();

        /// <summary>
        /// Requests a single playlist, or <c>null</c> if it does not exist.
        /// </summary>
        Task<DtoPlaylist?> RequestPlaylistAsync(long playlistId);

        /// <summary>
        /// Creates a new playlist.
        /// </summary>
        Task<DtoPlaylist> CreatePlaylistAsync(DtoCreatePlaylistRequest request);

        /// <summary>
        /// Updates an existing playlist.
        /// </summary>
        Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, DtoUpdatePlaylistRequest request);

        /// <summary>
        /// Deletes a playlist.
        /// </summary>
        Task DeletePlaylistAsync(long playlistId);

        /// <summary>
        /// Requests a sorted, paginated page of entries of a playlist.
        /// </summary>
        Task<DtoPlaylistEntriesPagedResult> RequestPlaylistEntriesPagedAsync(long playlistId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a media entry to a playlist.
        /// </summary>
        Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, DtoAddMediaToPlaylistRequest request);

        /// <summary>
        /// Removes a media entry from a playlist.
        /// </summary>
        Task RemoveMediaFromPlaylistAsync(long playlistId, string mediaType, long mediaId);

        /// <summary>
        /// Changes the manual sort order of a single entry of a playlist.
        /// </summary>
        Task ReorderPlaylistEntryAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest request);

        /// <summary>
        /// Atomically changes the manual sort order of multiple entries of a playlist.
        /// </summary>
        Task<IEnumerable<DtoPlaylistEntry>> BatchReorderPlaylistEntriesAsync(long playlistId, DtoBatchReorderPlaylistEntriesRequest request);

        /// <summary>
        /// Changes the sort mode of a playlist.
        /// </summary>
        Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, DtoChangeSortModeRequest request);

        /// <summary>
        /// Requests the current maximum manual sort order across the entire playlist (not just a
        /// loaded/virtualized page of it), for the "move to end" quick action.
        /// </summary>
        Task<long?> RequestMaxSortOrderAsync(long playlistId);

        /// <summary>
        /// Moves a single entry of a playlist to the true beginning of the manual order (shifting every
        /// other entry's manual sort order up by one server-side first), for the "move to beginning" quick
        /// action.
        /// </summary>
        Task<DtoPlaylistEntry> MoveEntryToBeginningAsync(long playlistId, long entryId);

        /// <summary>
        /// Moves a single entry of a playlist to an arbitrary target position, shifting every other entry
        /// between the entry's current and target position by one server-side first. Used by drag & drop
        /// reordering.
        /// </summary>
        Task MoveEntryBetweenAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest request);
    }
}
