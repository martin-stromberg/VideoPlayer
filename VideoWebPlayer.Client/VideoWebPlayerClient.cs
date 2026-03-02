using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Data;
using Microsoft.Extensions.Configuration;

namespace VideoWebPlayer.Client
{
    public class VideoWebPlayerClient
    {
        private readonly HttpClient httpClient;
        private readonly ConcurrentDictionary<string, ProgressSendState> progressStates = new();
        
        /// <summary>
        /// Event wird ausgelöst, wenn ein 401 Unauthorized empfangen wird (Token abgelaufen).
        /// </summary>
        public event EventHandler? UnauthorizedReceived;

        public VideoWebPlayerClient(HttpClient httpClient, ILogger<VideoWebPlayerClient> logger)
        {
            this.httpClient = httpClient;
            Logger = logger;
        }

        private sealed class ProgressSendState
        {
            public readonly object Gate = new();
            public bool IsSending { get; set; }
            public ProgressPayload? Buffered { get; set; }
        }

        private sealed class ProgressPayload
        {
            public required string MediaType { get; init; }
            public required long MediaId { get; init; }
            public required long PositionSeconds { get; init; }
            public required long DurationSeconds { get; init; }
        }

        protected virtual async Task<T> HttpGetAsync<T>(string endPoint)
        {
            var response = await httpClient.GetAsync(endPoint);
            
            // Prüfe auf 401 Unauthorized
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Logger?.LogWarning("Received 401 Unauthorized from {EndPoint}. Token might be expired.", endPoint);
                UnauthorizedReceived?.Invoke(this, EventArgs.Empty);
                throw new HttpRequestException($"Unauthorized: {endPoint}");
            }
            
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
            
            // Prüfe auf 401 Unauthorized
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Logger?.LogWarning("Received 401 Unauthorized from {EndPoint}. Token might be expired.", endPoint);
                UnauthorizedReceived?.Invoke(this, EventArgs.Empty);
                throw new HttpRequestException($"Unauthorized: {endPoint}");
            }
            
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to POST from {endPoint}: {response.ReasonPhrase}");
            return System.Text.Json.JsonSerializer.Deserialize<T>(content, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Deserialization returned null.");
        }

        #region Authentication
        /// <summary>
        /// Authenticates the user and stores the authorization token.
        /// </summary>
        public async Task<AuthorizationToken?> AuthenticateAsync(string email, string password)
        {
            var request = new AuthenticationRequest { Email = email, Password = password };
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var token = await HttpPostAsync<AuthorizationToken>("api/auth/login", content);
            SetAuthorizationToken(token);
            return token;
        }
        
        public virtual void SetAuthorizationToken(AuthorizationToken token)
        {
            if (token is not null)
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.token);
            }
        }
        
        public string? AuthorizationToken
        {
            get
            {
                return httpClient.DefaultRequestHeaders.Authorization?.Parameter;
            }
        }
        
        public bool Initializing { get; set; }
        protected ILogger<VideoWebPlayerClient> Logger { get; }
        #endregion

        #region HealthCheck
        /// <summary>
        /// Checks if the server is reachable and healthy.
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var response = await httpClient.GetAsync("api/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
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
        public async Task<DtoMediaSource?> RequestSourceAsync(long sourceId)
        {
            try
            {
                return await HttpGetAsync<DtoMediaSource>($"api/Sources/{sourceId}");
            }
            catch
            {
                return null;
            }
        }
        public async Task<SourceGenresDto?> RequestSourceGenresAsync(long sourceId)
        {
            try
            {
                return await HttpGetAsync<SourceGenresDto>($"api/SourceGenres/{sourceId}");
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region Recent Entries
        public async Task<IEnumerable<DtoRecentEntry>> RequestRecentEntriesAsync()
        {
            return await HttpGetAsync<DtoRecentEntry[]>("api/items/recent");
        }
        #endregion

        #region Continue Watching
        public async Task<IEnumerable<ContinueWatchingDto>> RequestContinueWatchingAsync()
        {
            try
            {
                return await HttpGetAsync<ContinueWatchingDto[]>("api/continue-watching");
            }
            catch
            {
                return Array.Empty<ContinueWatchingDto>();
            }
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
        public async Task<List<MediaEntryDto>> RequestSourceItems(long mediaSourceId, int Page = 0, int PageSize = 30, string searchText = "", long genreId = 0)
        {
            var url = $"/api/items?mediaSourceId={mediaSourceId}&page={Page}&size={PageSize}";
            if (!string.IsNullOrWhiteSpace(searchText))
                url += $"&search={Uri.EscapeDataString(searchText)}";
            if (genreId > 0)
                url += $"&genreId={genreId}";
            return await HttpGetAsync<List<MediaEntryDto>>(url);
        }

        public async Task<DtoMediaEntry> RequestMovieCollectionAsync(long id)
        {
            return await HttpGetAsync<DtoMovieCollection>($"api/items/moviecollection/{id}");
        }
        public async Task<DtoMediaEntry> RequestTVShowAsync(long id)
        {
            return await HttpGetAsync<DtoTVShow>($"api/items/tvshow/{id}");
        }
        
        #endregion
        
        #region Continue Watching
        /// <summary>
        /// Gets the current user's continue-watching list.
        /// </summary>
        public async Task<List<ContinueWatchingDto>> GetContinueWatchingAsync()
        {
            return await HttpGetAsync<List<ContinueWatchingDto>>("api/continue-watching");
        }
        
        /// <summary>
        /// Reports playback progress for the current user.
        /// Coalescing: Für ein MediaItem läuft nur ein Request gleichzeitig.
        /// Währenddessen wird genau eine letzte Meldung gepuffert (überschreibend).
        /// </summary>
        public async Task ReportPlaybackProgressAsync(string mediaType, long mediaId, long positionSeconds, long durationSeconds)
        {
            var key = $"{mediaType}:{mediaId}";
            var payload = new ProgressPayload
            {
                MediaType = mediaType,
                MediaId = mediaId,
                PositionSeconds = positionSeconds,
                DurationSeconds = durationSeconds
            };

            var state = progressStates.GetOrAdd(key, _ => new ProgressSendState());

            bool shouldSendNow;
            lock (state.Gate)
            {
                if (!state.IsSending)
                {
                    state.IsSending = true;
                    shouldSendNow = true;
                }
                else
                {
                    state.Buffered = payload;
                    shouldSendNow = false;
                }
            }

            if (!shouldSendNow)
                return;

            var current = payload;
            try
            {
                while (true)
                {
                    await SendPlaybackProgressCoreAsync(current);

                    ProgressPayload? next;
                    lock (state.Gate)
                    {
                        next = state.Buffered;
                        state.Buffered = null;

                        if (next is null)
                        {
                            state.IsSending = false;
                            break;
                        }
                    }

                    current = next;
                }
            }
            finally
            {
                lock (state.Gate)
                {
                    if (!state.IsSending && state.Buffered is null)
                    {
                        progressStates.TryRemove(key, out _);
                    }
                }
            }
        }

        private async Task SendPlaybackProgressCoreAsync(ProgressPayload payload)
        {
            var body = new
            {
                MediaType = payload.MediaType,
                MediaId = payload.MediaId,
                PositionSeconds = payload.PositionSeconds,
                DurationSeconds = payload.DurationSeconds
            };

            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("api/continue-watching/progress", content);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Logger?.LogWarning("Received 401 Unauthorized from continue-watching/progress. Token might be expired.");
                UnauthorizedReceived?.Invoke(this, EventArgs.Empty);
                throw new HttpRequestException("Unauthorized: api/continue-watching/progress");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to report progress: {response.ReasonPhrase}");
            }
        }
        #endregion
    }
}
