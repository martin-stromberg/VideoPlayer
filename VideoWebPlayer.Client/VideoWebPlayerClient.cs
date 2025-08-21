
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Client
{
    public class VideoWebPlayerClient
    {
        private readonly HttpClient httpClient;

        public VideoWebPlayerClient(HttpClient httpClient, ILogger<VideoWebPlayerClient> logger)
        {
            this.httpClient = httpClient;
            Logger = logger;
        }
        protected virtual async Task<T> HttpGetAsync<T>(string endPoint)
        {
            var response = await httpClient.GetAsync(endPoint);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to GET from {endPoint}: {response.ReasonPhrase}");
            return System.Text.Json.JsonSerializer.Deserialize<T>(content, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Deserialization returned null.");
        }
        protected virtual async Task<T> HttpPostAsync<T>(string endPoint, HttpContent args)
        {
            var response = await httpClient.PostAsync(endPoint, args);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to POST from {endPoint}: {response.ReasonPhrase}");
            return System.Text.Json.JsonSerializer.Deserialize<T>(content, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Deserialization returned null.");
        }

        #region Authentication
        public void Authenticate(string email, string password)
        {
            throw new NotImplementedException();
        }
        public void SetAuthorizationToken(AuthorizationToken token)
        {
            if (token is not null)
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.token);
        }
        public string AuthorizationToken
        {
            get => httpClient.DefaultRequestHeaders.Authorization?.Parameter;
        }
        public bool Initializing { get; set; }
        protected ILogger<VideoWebPlayerClient> Logger { get; }
        #endregion

        #region Sources
        public async Task<IEnumerable<DtoMediaSource>> RequestSourcesAsync()
        {
            try
            {
                return await HttpGetAsync<DtoMediaSource[]>("api/Sources");
            }
            catch
            {
                return new DtoMediaSource[0];
            }
        }
        #endregion

        #region Recent Entries
        public async Task<IEnumerable<DtoRecentEntry>> RequestRecentEntriesAsync()
        {
            return await HttpGetAsync<DtoRecentEntry[]>("api/items/recent");
        }
        #endregion

        #region Favorites
        public async Task<IEnumerable<DtoFavoriteEntry>> RequestFavoritesAsync()
        {
            return await HttpGetAsync<DtoFavoriteEntry[]>("api/favorites");
        }
        public async Task<bool> ToggleFavorite(DtoMediaEntry entry)
        {
            var json = JsonSerializer.Serialize(entry);
            return await HttpPostAsync<bool>("api/favorites/toggle", new StringContent(json, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
        }
        #endregion

        #region Media Entries
        public async Task<DtoMediaEntry> RequestMovieCollectionAsync(long id)
        {
            return await HttpGetAsync<DtoMovieCollection>($"api/items/moviecollection/{id}");
        }
        public async Task<DtoMediaEntry> RequestTVShowAsync(long id)
        {
            return await HttpGetAsync<DtoTVShow>($"api/items/tvshow/{id}");
        }
        
        #endregion
    }
}
