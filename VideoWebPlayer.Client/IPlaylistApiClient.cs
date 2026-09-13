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
        /// <returns>The current user's playlists.</returns>
        Task<IEnumerable<DtoPlaylist>> RequestPlaylistsAsync();

        /// <summary>
        /// Requests a single playlist, or <c>null</c> if it does not exist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <returns>The playlist, or <c>null</c> if it does not exist.</returns>
        Task<DtoPlaylist?> RequestPlaylistAsync(long playlistId);

        /// <summary>
        /// Creates a new playlist.
        /// </summary>
        /// <param name="request">Data of the playlist to create.</param>
        /// <returns>The created playlist.</returns>
        Task<DtoPlaylist> CreatePlaylistAsync(DtoCreatePlaylistRequest request);

        /// <summary>
        /// Updates an existing playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist to update.</param>
        /// <param name="request">Data to update the playlist with.</param>
        /// <returns>The updated playlist.</returns>
        Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, DtoUpdatePlaylistRequest request);

        /// <summary>
        /// Deletes a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist to delete.</param>
        Task DeletePlaylistAsync(long playlistId);

        /// <summary>
        /// Requests a sorted, paginated page of entries of a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="pageNumber">1-based number of the page to request.</param>
        /// <param name="pageSize">Maximum number of entries per page.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>The requested page of playlist entries.</returns>
        Task<DtoPlaylistEntriesPagedResult> RequestPlaylistEntriesPagedAsync(long playlistId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a media entry to a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist to add the media entry to.</param>
        /// <param name="request">Data describing the media entry to add.</param>
        /// <returns>The result of the add operation.</returns>
        Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, DtoAddMediaToPlaylistRequest request);

        /// <summary>
        /// Removes a media entry from a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist to remove the media entry from.</param>
        /// <param name="mediaType">Type of the media entry to remove.</param>
        /// <param name="mediaId">Id of the media entry to remove.</param>
        Task RemoveMediaFromPlaylistAsync(long playlistId, string mediaType, long mediaId);

        /// <summary>
        /// Changes the manual sort order of a single entry of a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="entryId">Id of the entry to reorder.</param>
        /// <param name="request">Data describing the new sort order.</param>
        Task ReorderPlaylistEntryAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest request);

        /// <summary>
        /// Atomically changes the manual sort order of multiple entries of a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="request">Data describing the new sort orders.</param>
        /// <returns>The reordered playlist entries.</returns>
        Task<IEnumerable<DtoPlaylistEntry>> BatchReorderPlaylistEntriesAsync(long playlistId, DtoBatchReorderPlaylistEntriesRequest request);

        /// <summary>
        /// Changes the sort mode of a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="request">Data describing the new sort mode.</param>
        /// <returns>The updated playlist.</returns>
        Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, DtoChangeSortModeRequest request);

        /// <summary>
        /// Requests the current maximum manual sort order across the entire playlist (not just a
        /// loaded/virtualized page of it), for the "move to end" quick action.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <returns>The current maximum manual sort order, or <c>null</c> if the playlist has no entries.</returns>
        Task<long?> RequestMaxSortOrderAsync(long playlistId);

        /// <summary>
        /// Moves a single entry of a playlist to the true beginning of the manual order (shifting every
        /// other entry's manual sort order up by one server-side first), for the "move to beginning" quick
        /// action.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="entryId">Id of the entry to move.</param>
        /// <returns>The moved playlist entry.</returns>
        Task<DtoPlaylistEntry> MoveEntryToBeginningAsync(long playlistId, long entryId);

        /// <summary>
        /// Moves a single entry of a playlist to an arbitrary target position, shifting every other entry
        /// between the entry's current and target position by one server-side first. Used by drag &amp; drop
        /// reordering.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="entryId">Id of the entry to move.</param>
        /// <param name="request">Data describing the target position.</param>
        Task MoveEntryBetweenAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest request);

        /// <summary>
        /// Starts playback of a playlist at the given entry, or at the first playable and accessible
        /// entry if <paramref name="entryId"/> is <c>null</c>.
        /// </summary>
        /// <param name="playlistId">Id of the playlist to start playback of.</param>
        /// <param name="entryId">Id of the entry to start at, or <c>null</c> to start at the first playable and accessible entry.</param>
        /// <returns>The playback start information.</returns>
        Task<DtoPlaylistPlaybackStart> StartPlaylistAsync(long playlistId, long? entryId);

        /// <summary>
        /// Requests the next playable and accessible entry after <paramref name="currentEntryId"/>,
        /// together with its actual position in the playlist, for manual "next" navigation within a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="currentEntryId">Id of the entry currently being played.</param>
        /// <returns>The next playable and accessible entry, or <c>null</c> if there is none.</returns>
        Task<DtoPlaylistNavigationResult?> GetNextPlaylistEntryAsync(long playlistId, long currentEntryId);

        /// <summary>
        /// Requests the previous playable and accessible entry before <paramref name="currentEntryId"/>,
        /// together with its actual position in the playlist, for manual "previous" navigation within a playlist.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="currentEntryId">Id of the entry currently being played.</param>
        /// <returns>The previous playable and accessible entry, or <c>null</c> if there is none.</returns>
        Task<DtoPlaylistNavigationResult?> GetPreviousPlaylistEntryAsync(long playlistId, long currentEntryId);

        /// <summary>
        /// Advances playback from <paramref name="currentEntryId"/> to the next entry, together with its
        /// actual position in the playlist, for automatic advance when a title finishes playing.
        /// </summary>
        /// <param name="playlistId">Id of the playlist.</param>
        /// <param name="currentEntryId">Id of the entry that just finished playing.</param>
        /// <returns>The next playable and accessible entry, or <c>null</c> if there is none.</returns>
        Task<DtoPlaylistNavigationResult?> AdvancePlaylistAsync(long playlistId, long currentEntryId);
    }
}
