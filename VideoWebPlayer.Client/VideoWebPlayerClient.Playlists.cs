using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Client
{
    // Implements IPlaylistApiClient. Physically separated from VideoWebPlayerClient.cs (see the class
    // summary there) while sharing its HttpGet/Post/Put/PatchAsync helpers, reauthorization handling and
    // CreateJsonContent helper.
    public partial class VideoWebPlayerClient
    {
        /// <inheritdoc />
        public async Task<IEnumerable<DtoPlaylist>> RequestPlaylistsAsync(long? genreId = null)
        {
            var query = genreId.HasValue ? $"?genreId={genreId.Value}" : string.Empty;
            return await HttpGetAsync<DtoPlaylist[]>($"api/playlists{query}");
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DtoPlaylist>> RequestPublicPlaylistsAsync(long? genreId = null)
        {
            var query = genreId.HasValue ? $"?genreId={genreId.Value}" : string.Empty;
            return await HttpGetAsync<DtoPlaylist[]>($"api/playlists/public{query}");
        }

        /// <inheritdoc />
        public async Task<DtoPlaylist> SetPlaylistPublicAsync(long playlistId, DtoSetPlaylistPublicRequest request)
        {
            return await HttpPutAsync<DtoPlaylist>($"api/playlists/{playlistId}/public", CreateJsonContent(request));
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
        public async Task RemoveMediaFromPlaylistAsync(long playlistId, string mediaType, long mediaId, bool confirmContinueWatchingRemoval = false)
        {
            var query = confirmContinueWatchingRemoval ? "?confirmContinueWatchingRemoval=true" : string.Empty;
            await HttpDeleteAsync($"api/playlists/{playlistId}/entries/{mediaType}/{mediaId}{query}");
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
        public async Task<DtoPlaylist> SetPlaylistGenresAsync(long playlistId, DtoSetPlaylistGenresRequest request)
        {
            return await HttpPutAsync<DtoPlaylist>($"api/playlists/{playlistId}/genres", CreateJsonContent(request));
        }

        /// <inheritdoc />
        public async Task<DtoPlaylist> ResetPlaylistGenresAsync(long playlistId)
        {
            return await HttpPostAsync<DtoPlaylist>($"api/playlists/{playlistId}/genres/reset", new StringContent(string.Empty));
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

        /// <inheritdoc />
        public async Task MoveEntryBetweenAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest request)
        {
            await HttpPostAsync($"api/playlists/{playlistId}/entries/{entryId}/move-between", CreateJsonContent(request));
        }

        /// <inheritdoc />
        public async Task<DtoPlaylistPlaybackStart> StartPlaylistAsync(long playlistId, long? entryId)
        {
            var query = entryId.HasValue ? $"?entryId={entryId.Value}" : string.Empty;
            return await HttpPostAsync<DtoPlaylistPlaybackStart>($"api/playlists/{playlistId}/play{query}", new StringContent(string.Empty));
        }

        /// <inheritdoc />
        public Task<DtoPlaylistNavigationResult?> GetNextPlaylistEntryAsync(long playlistId, long currentEntryId)
            => PostForOptionalPlaylistNavigationResultAsync($"api/playlists/{playlistId}/play/next?currentEntryId={currentEntryId}");

        /// <inheritdoc />
        public Task<DtoPlaylistNavigationResult?> GetPreviousPlaylistEntryAsync(long playlistId, long currentEntryId)
            => PostForOptionalPlaylistNavigationResultAsync($"api/playlists/{playlistId}/play/previous?currentEntryId={currentEntryId}");

        /// <inheritdoc />
        public Task<DtoPlaylistNavigationResult?> AdvancePlaylistAsync(long playlistId, long currentEntryId)
            => PostForOptionalPlaylistNavigationResultAsync($"api/playlists/{playlistId}/play/advance?currentEntryId={currentEntryId}");

        /// <summary>
        /// POSTs to an endpoint that responds with either a <see cref="DtoPlaylistNavigationResult"/> or
        /// 204 No Content (end/beginning of playlist reached), shared by
        /// <see cref="GetNextPlaylistEntryAsync"/>, <see cref="GetPreviousPlaylistEntryAsync"/> and
        /// <see cref="AdvancePlaylistAsync"/>, via <see cref="SendAndDeserializeAsync{T}"/>'s
        /// <c>treatNoContentAsNull</c> option.
        /// </summary>
        /// <param name="endPoint">The relative endpoint to POST to.</param>
        /// <returns>The deserialized navigation result, or <c>null</c> if the server responded with 204 No Content.</returns>
        private Task<DtoPlaylistNavigationResult?> PostForOptionalPlaylistNavigationResultAsync(string endPoint)
            => SendAndDeserializeAsync<DtoPlaylistNavigationResult?>(
                endPoint,
                "POST",
                () => httpClient.PostAsync(endPoint, new StringContent(string.Empty)),
                treatNoContentAsNull: true);

        /// <inheritdoc />
        public async Task<DtoPlaylistCoverResult> UploadPlaylistCoverAsync(long playlistId, byte[] fileContent, string fileName, string contentType)
        {
            using var content = new MultipartFormDataContent();
            var fileContentPart = new ByteArrayContent(fileContent);
            fileContentPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(fileContentPart, "file", fileName);

            return await HttpPostAsync<DtoPlaylistCoverResult>($"api/playlists/{playlistId}/cover/upload", content);
        }

        /// <inheritdoc />
        public async Task<DtoPlaylistCoverResult> RegeneratePlaylistCoverAsync(long playlistId, bool confirmReplaceUploadedCover = false)
        {
            var query = confirmReplaceUploadedCover ? "?confirmReplaceUploadedCover=true" : string.Empty;
            return await HttpPostAsync<DtoPlaylistCoverResult>($"api/playlists/{playlistId}/cover/regenerate{query}", new StringContent(string.Empty));
        }

        /// <inheritdoc />
        public async Task<DtoPlaylistCoverPreview> PreviewPlaylistCoverAsync(long playlistId)
        {
            return await HttpPostAsync<DtoPlaylistCoverPreview>($"api/playlists/{playlistId}/cover/preview", new StringContent(string.Empty));
        }

        /// <inheritdoc />
        public async Task<DtoPlaylistCoverResult> DeletePlaylistCoverAsync(long playlistId)
        {
            return await HttpDeleteAsync<DtoPlaylistCoverResult>($"api/playlists/{playlistId}/cover");
        }
    }
}
