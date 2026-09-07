using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Client
{
    // Implements IPlaylistApiClient. Physically separated from VideoWebPlayerClient.cs (see the class
    // summary there) while sharing its HttpGet/Post/Put/PatchAsync helpers, reauthorization handling and
    // CreateJsonContent helper.
    public partial class VideoWebPlayerClient
    {
        /// <inheritdoc />
        public async Task<IEnumerable<DtoPlaylist>> RequestPlaylistsAsync()
        {
            return await HttpGetAsync<DtoPlaylist[]>("api/playlists");
        }

        /// <inheritdoc />
        public async Task<DtoPlaylist?> RequestPlaylistAsync(long playlistId)
        {
            try
            {
                return await HttpGetAsync<DtoPlaylist>($"api/playlists/{playlistId}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<DtoPlaylist> CreatePlaylistAsync(DtoCreatePlaylistRequest request)
        {
            return await HttpPostAsync<DtoPlaylist>("api/playlists", CreateJsonContent(request));
        }

        /// <inheritdoc />
        public async Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, DtoUpdatePlaylistRequest request)
        {
            return await HttpPutAsync<DtoPlaylist>($"api/playlists/{playlistId}", CreateJsonContent(request));
        }

        /// <inheritdoc />
        public async Task DeletePlaylistAsync(long playlistId)
        {
            await HttpDeleteAsync($"api/playlists/{playlistId}");
        }

        /// <inheritdoc />
        public async Task<DtoPlaylistEntriesPagedResult> RequestPlaylistEntriesPagedAsync(long playlistId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await HttpGetAsync<DtoPlaylistEntriesPagedResult>($"api/playlists/{playlistId}/entries/paged?pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);
        }

        /// <inheritdoc />
        public async Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, DtoAddMediaToPlaylistRequest request)
        {
            return await HttpPostAsync<DtoPlaylistAddResult>($"api/playlists/{playlistId}/entries", CreateJsonContent(request));
        }

        /// <inheritdoc />
        public async Task RemoveMediaFromPlaylistAsync(long playlistId, string mediaType, long mediaId)
        {
            await HttpDeleteAsync($"api/playlists/{playlistId}/entries/{mediaType}/{mediaId}");
        }

        /// <inheritdoc />
        public async Task ReorderPlaylistEntryAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest request)
        {
            await HttpPutAsync($"api/playlists/{playlistId}/entries/{entryId}/order", CreateJsonContent(request));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DtoPlaylistEntry>> BatchReorderPlaylistEntriesAsync(long playlistId, DtoBatchReorderPlaylistEntriesRequest request)
        {
            return await HttpPostAsync<DtoPlaylistEntry[]>($"api/playlists/{playlistId}/entries/batch-reorder", CreateJsonContent(request));
        }

        /// <inheritdoc />
        public async Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, DtoChangeSortModeRequest request)
        {
            return await HttpPatchAsync<DtoPlaylist>($"api/playlists/{playlistId}/sort-mode", CreateJsonContent(request));
        }

        /// <inheritdoc />
        public async Task<long?> RequestMaxSortOrderAsync(long playlistId)
        {
            var result = await HttpGetAsync<DtoMaxSortOrderResult>($"api/playlists/{playlistId}/entries/max-sort-order");
            return result.MaxSortOrder;
        }

        /// <inheritdoc />
        public async Task<DtoPlaylistEntry> MoveEntryToBeginningAsync(long playlistId, long entryId)
        {
            return await HttpPostAsync<DtoPlaylistEntry>($"api/playlists/{playlistId}/entries/{entryId}/move-to-beginning", new StringContent(string.Empty));
        }
    }
}
