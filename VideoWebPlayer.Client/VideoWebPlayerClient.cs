using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Client
{
    /// <summary>
    /// Facade over the server API. The Playlist operations (<see cref="IPlaylistApiClient"/>) live in the
    /// <c>VideoWebPlayerClient.Playlists.cs</c> partial file, physically separating that cohesive group of
    /// endpoints from the other regions (Authentication, Sources, Actors, Continue Watching, Favorites,
    /// Media Entries, ...) while still sharing this class's HTTP/reauthorization infrastructure and the
    /// impersonation behavior of <c>InternalVideoWebPlayerClient</c>.
    /// </summary>
    public partial class VideoWebPlayerClient : IPlaylistApiClient
    {
        private readonly HttpClient httpClient;
        private readonly ConcurrentDictionary<string, ProgressSendState> progressStates = new();

        /// <summary>
        /// Creates a new client instance backed by the given <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="httpClient">The HTTP client used to call the server API.</param>
        /// <param name="logger">The logger used to record diagnostic information.</param>
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

        /// <summary>
        /// Attempts to obtain a fresh authorization token after the server responded with 401
        /// Unauthorized. The default implementation does nothing and reports failure; derived classes
        /// may impersonate or otherwise renew the token.
        /// </summary>
        /// <returns><c>true</c> if a new token was obtained and the failed request should be retried; otherwise <c>false</c>.</returns>
        protected virtual Task<bool> HandleUnauthorized()
        {
            return Task.FromResult(false);
        }

        // Executes an HTTP request, retrying once via HandleUnauthorized if the server responds with
        // 401 Unauthorized. Shared by all Http*Async helper methods.
        private async Task<HttpResponseMessage> SendWithReauthorizationAsync(string endPoint, Func<Task<HttpResponseMessage>> doRequestAsync, bool skipReauthorize = false)
        {
            var response = await doRequestAsync();
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                return response;

            Logger?.LogWarning("Received 401 Unauthorized from {EndPoint}. Token might be expired.", endPoint);

            if (skipReauthorize)
            {
                // We're in the login call itself; do not attempt to re-authorize.
                Logger?.LogWarning("Skipping reauthorization for request to {EndPoint}.", endPoint);
                throw new HttpRequestException($"Unauthorized: {endPoint}", null, System.Net.HttpStatusCode.Unauthorized);
            }

            if (!await HandleUnauthorized())
                // Kein neuer Token innerhalb der Wartezeit
                throw new HttpRequestException($"Unauthorized: {endPoint}", null, System.Net.HttpStatusCode.Unauthorized);

            response = await doRequestAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Wenn nach Erneuerung weiterhin Unauthorized kommt, gib das weiter.
                Logger?.LogWarning("Retry after token refresh still returned 401 for {EndPoint}.", endPoint);
                throw new HttpRequestException($"Unauthorized: {endPoint}", null, System.Net.HttpStatusCode.Unauthorized);
            }

            return response;
        }

        // Executes a request via SendWithReauthorizationAsync, then reads the response body and either
        // throws (on failure) or deserializes it to T. Shared by HttpGetAsync, HttpPostAsync, HttpPutAsync
        // and PostForOptionalPlaylistEntryAsync. When treatNoContentAsNull is set, a 204 No Content response
        // short-circuits to default(T) (null for the reference-typed DTOs this is used with) instead of
        // attempting to deserialize an empty body.
        private async Task<T> SendAndDeserializeAsync<T>(string endPoint, string httpMethod, Func<Task<HttpResponseMessage>> doRequestAsync, bool skipReauthorize = false, bool treatNoContentAsNull = false)
        {
            var response = await SendWithReauthorizationAsync(endPoint, doRequestAsync, skipReauthorize);

            if (treatNoContentAsNull && response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return default!;

            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(content)
                        ? $"Failed to {httpMethod} from {endPoint}: {response.ReasonPhrase}"
                        : content,
                    null,
                    response.StatusCode);
            return System.Text.Json.JsonSerializer.Deserialize<T>(content, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Deserialization returned null.");
        }

        /// <summary>
        /// Sends a GET request to <paramref name="endPoint"/> and deserializes the JSON response body to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response body into.</typeparam>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <returns>The deserialized response body.</returns>
        protected virtual Task<T> HttpGetAsync<T>(string endPoint)
        {
            return SendAndDeserializeAsync<T>(endPoint, "GET", () => httpClient.GetAsync(endPoint));
        }

        /// <summary>
        /// Sends a GET request to <paramref name="endPoint"/> and deserializes the JSON response body to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response body into.</typeparam>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>The deserialized response body.</returns>
        protected virtual Task<T> HttpGetAsync<T>(string endPoint, CancellationToken cancellationToken)
        {
            return SendAndDeserializeAsync<T>(endPoint, "GET", () => httpClient.GetAsync(endPoint, cancellationToken));
        }

        /// <summary>
        /// Sends a POST request with <paramref name="args"/> as the body to <paramref name="endPoint"/> and
        /// deserializes the JSON response body to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response body into.</typeparam>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <param name="args">The request body content.</param>
        /// <param name="skipReauthorize">If <c>true</c>, does not attempt to reauthorize on a 401 response (used for the login call itself).</param>
        /// <returns>The deserialized response body.</returns>
        protected virtual Task<T> HttpPostAsync<T>(string endPoint, HttpContent args, bool skipReauthorize = false)
        {
            return SendAndDeserializeAsync<T>(endPoint, "POST", () => httpClient.PostAsync(endPoint, args), skipReauthorize);
        }

        /// <summary>
        /// Sends a PUT request with <paramref name="args"/> as the body to <paramref name="endPoint"/> and
        /// deserializes the JSON response body to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response body into.</typeparam>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <param name="args">The request body content.</param>
        /// <returns>The deserialized response body.</returns>
        protected virtual Task<T> HttpPutAsync<T>(string endPoint, HttpContent args)
        {
            return SendAndDeserializeAsync<T>(endPoint, "PUT", () => httpClient.PutAsync(endPoint, args));
        }

        /// <summary>
        /// Sends a PUT request with <paramref name="args"/> as the body to <paramref name="endPoint"/>, without
        /// deserializing a response body.
        /// </summary>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <param name="args">The request body content.</param>
        protected virtual async Task HttpPutAsync(string endPoint, HttpContent args)
        {
            var response = await SendWithReauthorizationAsync(endPoint, () => httpClient.PutAsync(endPoint, args));

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(content)
                        ? $"Failed to PUT from {endPoint}: {response.ReasonPhrase}"
                        : content,
                    null,
                    response.StatusCode);
            }
        }

        /// <summary>
        /// Sends a PATCH request with <paramref name="args"/> as the body to <paramref name="endPoint"/> and
        /// deserializes the JSON response body to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response body into.</typeparam>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <param name="args">The request body content.</param>
        /// <returns>The deserialized response body.</returns>
        protected virtual Task<T> HttpPatchAsync<T>(string endPoint, HttpContent args)
        {
            return SendAndDeserializeAsync<T>(endPoint, "PATCH", () => httpClient.PatchAsync(endPoint, args));
        }

        /// <summary>
        /// Sends a POST request with <paramref name="args"/> as the body to <paramref name="endPoint"/>, without
        /// deserializing a response body.
        /// </summary>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <param name="args">The request body content.</param>
        /// <param name="skipReauthorize">If <c>true</c>, does not attempt to reauthorize on a 401 response (used for the login call itself).</param>
        protected virtual async Task HttpPostAsync(string endPoint, HttpContent args, bool skipReauthorize = false)
        {
            var response = await SendWithReauthorizationAsync(endPoint, () => httpClient.PostAsync(endPoint, args), skipReauthorize);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(content)
                        ? $"Failed to POST from {endPoint}: {response.ReasonPhrase}"
                        : content,
                    null,
                    response.StatusCode);
            }
        }

        /// <summary>
        /// Sends a DELETE request to <paramref name="endPoint"/> and deserializes the JSON response body to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response body into.</typeparam>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        /// <returns>The deserialized response body.</returns>
        protected virtual Task<T> HttpDeleteAsync<T>(string endPoint)
        {
            return SendAndDeserializeAsync<T>(endPoint, "DELETE", () => httpClient.DeleteAsync(endPoint));
        }

        /// <summary>
        /// Sends a DELETE request to <paramref name="endPoint"/>, without deserializing a response body.
        /// </summary>
        /// <param name="endPoint">The relative API endpoint to call.</param>
        protected virtual async Task HttpDeleteAsync(string endPoint)
        {
            var response = await SendWithReauthorizationAsync(endPoint, () => httpClient.DeleteAsync(endPoint));

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(content)
                        ? $"Failed to DELETE from {endPoint}: {response.ReasonPhrase}"
                        : content,
                    null,
                    response.StatusCode);
            }
        }

        #region Authentication
        /// <summary>
        /// Authenticates the user and stores the authorization token.
        /// </summary>
        /// <param name="email">The email address of the user to authenticate.</param>
        /// <param name="password">The password of the user to authenticate.</param>
        /// <returns>The obtained authorization token, or <c>null</c> if authentication failed.</returns>
        public async Task<AuthorizationToken?> AuthenticateAsync(string email, string password)
        {
            var request = new AuthenticationRequest { Email = email, Password = password };
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var token = await HttpPostAsync<AuthorizationToken>("api/auth/login", content, skipReauthorize: true);
            SetAuthorizationToken(token);
            return token;
        }

        /// <summary>
        /// Applies the given authorization token to the underlying <see cref="HttpClient"/> as a bearer token,
        /// so that subsequent requests are authenticated.
        /// </summary>
        /// <param name="token">The authorization token to apply.</param>
        public virtual void SetAuthorizationToken(AuthorizationToken token)
        {
            if (token is not null)
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.token);
            }
        }

        /// <summary>
        /// The bearer token currently applied to the underlying <see cref="HttpClient"/>, or <c>null</c>
        /// if none is set.
        /// </summary>
        public string? AuthorizationToken
        {
            get
            {
                return httpClient.DefaultRequestHeaders.Authorization?.Parameter;
            }
        }

        /// <summary>
        /// Ensures an authorization token is available for the current user.
        /// The default implementation does nothing; derived classes may impersonate.
        /// </summary>
        /// <param name="user">The current user's claims principal, or <c>null</c> if unauthenticated.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public virtual Task EnsureAuthorizationTokenAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Calls <see cref="EnsureAuthorizationTokenAsync(ClaimsPrincipal?, CancellationToken)"/> and
        /// silently ignores any exception (e.g. because no token is available for an unauthenticated
        /// user). The subsequent load operation then fails with a meaningful error message that is
        /// shown in the UI.
        /// </summary>
        /// <param name="user">The current user's claims principal, or <c>null</c> if unauthenticated.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task EnsureAuthorizationTokenSilentlyAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureAuthorizationTokenAsync(user, cancellationToken);
            }
            catch (Exception ex)
            {
                // Kein Token verfuegbar (z. B. nicht angemeldet). Der nachfolgende Ladevorgang
                // schlaegt dann mit einer aussagekraeftigen Fehlermeldung fehl, die im UI angezeigt wird.
                Logger?.LogDebug(ex, "EnsureAuthorizationTokenAsync fehlgeschlagen, wird stillschweigend ignoriert.");
            }
        }

        /// <summary>
        /// Indicates whether the client is currently performing its initial setup (e.g. impersonation),
        /// so that dependent UI can show a loading state.
        /// </summary>
        public bool Initializing { get; set; }

        /// <summary>
        /// The logger used to record diagnostic information for this client.
        /// </summary>
        protected ILogger<VideoWebPlayerClient> Logger { get; }
        #endregion

        #region HealthCheck
        /// <summary>
        /// Checks if the server is reachable and healthy.
        /// </summary>
        /// <returns><c>true</c> if the server responded with a successful status code; otherwise <c>false</c>.</returns>
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
        /// <summary>
        /// Requests all media sources.
        /// </summary>
        /// <returns>The media sources, or an empty collection if the request failed.</returns>
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
        /// <summary>
        /// Requests a single media source.
        /// </summary>
        /// <param name="sourceId">The identifier of the media source.</param>
        /// <returns>The media source, or <c>null</c> if it does not exist or the request failed.</returns>
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
        /// <summary>
        /// Requests the genre options available for a given media source.
        /// </summary>
        /// <param name="sourceId">The identifier of the media source.</param>
        /// <returns>The source's genres, or <c>null</c> if the request failed.</returns>
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

        #region Actors
        /// <summary>
        /// Requests a paginated, optionally filtered and sorted list of actors.
        /// </summary>
        /// <param name="search">An optional search text to filter actors by name.</param>
        /// <param name="sort">An optional sort specification.</param>
        /// <param name="filter">An optional filter specification.</param>
        /// <param name="offset">The number of actors to skip, for pagination.</param>
        /// <param name="limit">The maximum number of actors to return.</param>
        /// <returns>The matching actors, or an empty collection if the request failed.</returns>
        public async Task<IEnumerable<ActorDto>> RequestActorsAsync(string? search = null, string? sort = null, string? filter = null, int offset = 0, int limit = 50)
        {
            try
            {
                var query = new List<string>();
                if (!string.IsNullOrWhiteSpace(search))
                    query.Add($"search={Uri.EscapeDataString(search)}");
                if (!string.IsNullOrWhiteSpace(sort))
                    query.Add($"sort={Uri.EscapeDataString(sort)}");
                if (!string.IsNullOrWhiteSpace(filter))
                    query.Add($"filter={Uri.EscapeDataString(filter)}");
                query.Add($"offset={offset}");
                query.Add($"limit={limit}");
                var url = "api/Actors" + (query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty);
                return await HttpGetAsync<ActorDto[]>(url);
            }
            catch
            {
                return new ActorDto[0];
            }
        }

        /// <summary>
        /// Requests the details of a single actor.
        /// </summary>
        /// <param name="actorId">The identifier of the actor.</param>
        /// <returns>The actor's details, or <c>null</c> if it does not exist or the request failed.</returns>
        public async Task<ActorDetailsDto?> RequestActorAsync(long actorId)
        {
            try
            {
                return await HttpGetAsync<ActorDetailsDto>($"api/Actors/{actorId}");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Requests the actors that appear in a given movie.
        /// </summary>
        /// <param name="movieId">The identifier of the movie.</param>
        /// <returns>The movie's actors, or an empty collection if the request failed.</returns>
        public async Task<IEnumerable<ActorDto>> RequestActorsByMovieAsync(long movieId)
        {
            try
            {
                return await HttpGetAsync<ActorDto[]>($"api/Actors/by-movie/{movieId}");
            }
            catch
            {
                return new ActorDto[0];
            }
        }

        /// <summary>
        /// Requests the actors that appear in a given TV show episode.
        /// </summary>
        /// <param name="episodeId">The identifier of the episode.</param>
        /// <returns>The episode's actors, or an empty collection if the request failed.</returns>
        public async Task<IEnumerable<ActorDto>> RequestActorsByEpisodeAsync(long episodeId)
        {
            try
            {
                return await HttpGetAsync<ActorDto[]>($"api/Actors/by-episode/{episodeId}");
            }
            catch
            {
                return new ActorDto[0];
            }
        }

        /// <summary>
        /// Requests the available actor filter values.
        /// </summary>
        /// <param name="sort">An optional sort specification.</param>
        /// <returns>The available filter values, or an empty collection if the request failed.</returns>
        public async Task<IEnumerable<string>> RequestActorFiltersAsync(string? sort = null)
        {
            try
            {
                var query = !string.IsNullOrWhiteSpace(sort) ? $"?sort={Uri.EscapeDataString(sort)}" : string.Empty;
                return await HttpGetAsync<string[]>($"api/Actors/filters{query}");
            }
            catch
            {
                return new string[0];
            }
        }
        #endregion

        #region Recent Entries
        /// <summary>
        /// Requests the most recently added media entries.
        /// </summary>
        /// <returns>The recent entries.</returns>
        public async Task<IEnumerable<DtoRecentEntry>> RequestRecentEntriesAsync()
        {
            return await HttpGetAsync<DtoRecentEntry[]>("api/items/recent");
        }
        #endregion

        #region Continue Watching
        /// <summary>
        /// Requests the current user's continue-watching list.
        /// </summary>
        /// <returns>The continue-watching entries.</returns>
        public async Task<IEnumerable<ContinueWatchingDto>> RequestContinueWatchingAsync()
        {
            return await HttpGetAsync<ContinueWatchingDto[]>("api/continue-watching");
        }

        /// <summary>
        /// Hides a media entry from the current user's continue-watching list.
        /// </summary>
        /// <param name="mediaType">The type of the media entry (e.g. movie or episode).</param>
        /// <param name="mediaId">The identifier of the media entry.</param>
        /// <param name="playlistId">The playlist identifier of the entry, or <c>null</c> for a non-playlist entry.</param>
        /// <returns>The result of the mutation.</returns>
        public async Task<ContinueWatchingMutationResult> HideContinueWatchingAsync(string mediaType, long mediaId, long? playlistId = null)
        {
            var json = JsonSerializer.Serialize(new { MediaType = mediaType, MediaId = mediaId, PlaylistId = playlistId });
            return await HttpPostAsync<ContinueWatchingMutationResult>(
                "api/continue-watching/hide",
                new StringContent(json, System.Text.Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
        }

        /// <summary>
        /// Skips a media entry in the current user's continue-watching list (e.g. to move on to the next episode).
        /// </summary>
        /// <param name="mediaType">The type of the media entry (e.g. movie or episode).</param>
        /// <param name="mediaId">The identifier of the media entry.</param>
        /// <param name="playlistId">The playlist identifier of the entry, or <c>null</c> for a non-playlist entry.</param>
        /// <returns>The result of the mutation.</returns>
        public async Task<ContinueWatchingMutationResult> SkipContinueWatchingAsync(string mediaType, long mediaId, long? playlistId = null)
        {
            var json = JsonSerializer.Serialize(new { MediaType = mediaType, MediaId = mediaId, PlaylistId = playlistId });
            return await HttpPostAsync<ContinueWatchingMutationResult>(
                "api/continue-watching/skip",
                new StringContent(json, System.Text.Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
        }
        #endregion

        #region Favorites
        /// <summary>
        /// Requests the current user's favorite entries.
        /// </summary>
        /// <returns>The favorite entries.</returns>
        public async Task<IEnumerable<DtoFavoriteEntry>> RequestFavoritesAsync()
        {
            return await HttpGetAsync<DtoFavoriteEntry[]>("api/favorites");
        }
        /// <summary>
        /// Toggles whether a media entry is a favorite of the current user.
        /// </summary>
        /// <param name="entry">The media entry to toggle.</param>
        /// <returns><c>true</c> if the entry is now a favorite; <c>false</c> if it was removed from favorites.</returns>
        public async Task<bool> ToggleFavorite(DtoMediaEntry entry)
        {
            var json = JsonSerializer.Serialize(entry);
            return await HttpPostAsync<bool>("api/favorites/toggle", new StringContent(json, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
        }

        /// <summary>
        /// Requests the identifiers of the users for whom a media entry is unlocked.
        /// </summary>
        /// <param name="entry">The media entry to query.</param>
        /// <returns>The identifiers of the users the entry is unlocked for.</returns>
        public async Task<string[]> RequestUnlockedUserIdsAsync(DtoMediaEntry entry)
        {
            var json = JsonSerializer.Serialize(entry);
            return await HttpPostAsync<string[]>("api/UnlockedMedia/users", new StringContent(json, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
        }

        /// <summary>
        /// Sets the users for whom a media entry is unlocked.
        /// </summary>
        /// <param name="entry">The media entry to update.</param>
        /// <param name="userIds">The identifiers of the users the entry should be unlocked for.</param>
        public async Task SetUnlockedUsersAsync(DtoMediaEntry entry, string[] userIds)
        {
            var request = new UnlockedUsersRequest { Entry = entry, UserIds = userIds };
            var json = JsonSerializer.Serialize(request);
            await HttpPostAsync<string[]>("api/UnlockedMedia/set", new StringContent(json, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
        }

        /// <summary>
        /// Requests all users, for use in the unlocked-users selection UI.
        /// </summary>
        /// <returns>A dictionary mapping user identifiers to their display names.</returns>
        public async Task<Dictionary<string, string>> RequestAllUsersAsync()
        {
            var result = await HttpGetAsync<IEnumerable<UserIdName>>("api/UnlockedMedia/all-users");
            return result?.ToDictionary(u => u.Id, u => u.UserName ?? string.Empty) ?? new();
        }

        private sealed class UserIdName
        {
            public string Id { get; set; } = string.Empty;
            public string? UserName { get; set; }
        }

        /// <summary>
        /// Removes an entry from the current user's favorites.
        /// </summary>
        /// <param name="favoriteId">The identifier of the favorite entry to remove.</param>
        public async Task RemoveFavoriteAsync(long favoriteId)
        {
            var json = JsonSerializer.Serialize(new { Id = favoriteId, UserId = "anonymous" });
            await HttpPostAsync("api/favorites/remove", new StringContent(json, System.Text.Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")));
        }
        #endregion

        /// <summary>
        /// Serializes <paramref name="value"/> to JSON and wraps it in an <c>application/json</c>
        /// <see cref="StringContent"/>, for the POST/PUT/PATCH helper methods that send a JSON body.
        /// </summary>
        /// <typeparam name="T">The type of the value to serialize.</typeparam>
        /// <param name="value">The value to serialize as the request body.</param>
        /// <returns>The JSON-encoded <see cref="StringContent"/>.</returns>
        private static StringContent CreateJsonContent<T>(T value)
            => new(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");

        #region Media Entries
        // Shared URL-builder/HTTP-call for the "/api/items" endpoint, used by both RequestSourceItems
        // (media-source browsing, with genre filter) and RequestItemsAsync (cross-source name search).
        private Task<List<MediaEntryDto>> RequestItemsCoreAsync(long? mediaSourceId, int page, int size, string? search, long genreId, CancellationToken cancellationToken, bool includeIndividualMediaTypes = false)
        {
            var url = $"/api/items?page={page}&size={size}";
            if (mediaSourceId.HasValue)
                url += $"&mediaSourceId={mediaSourceId}";
            if (!string.IsNullOrWhiteSpace(search))
                url += $"&search={Uri.EscapeDataString(search)}";
            if (genreId > 0)
                url += $"&genreId={genreId}";
            if (includeIndividualMediaTypes)
                url += $"&includeIndividualMediaTypes={includeIndividualMediaTypes}";
            return HttpGetAsync<List<MediaEntryDto>>(url, cancellationToken);
        }

        /// <summary>
        /// Requests a page of the media entries of a given media source, optionally filtered by search text and genre.
        /// </summary>
        /// <param name="mediaSourceId">The identifier of the media source to browse.</param>
        /// <param name="Page">The zero-based page number to request.</param>
        /// <param name="PageSize">The number of entries per page.</param>
        /// <param name="searchText">An optional search text to filter entries by name.</param>
        /// <param name="genreId">An optional genre identifier to filter entries by, or <c>0</c> for no genre filter.</param>
        /// <returns>The requested page of media entries.</returns>
        public Task<List<MediaEntryDto>> RequestSourceItems(long mediaSourceId, int Page = 0, int PageSize = 30, string searchText = "", long genreId = 0)
            => RequestItemsCoreAsync(mediaSourceId, Page, PageSize, searchText, genreId, CancellationToken.None, includeIndividualMediaTypes: false);

        /// <summary>
        /// Requests a movie collection.
        /// </summary>
        /// <param name="id">The identifier of the movie collection.</param>
        /// <returns>The movie collection.</returns>
        public async Task<DtoMediaEntry> RequestMovieCollectionAsync(long id)
        {
            return await HttpGetAsync<DtoMovieCollection>($"api/items/moviecollection/{id}");
        }

        /// <summary>
        /// Requests a single movie.
        /// </summary>
        /// <param name="id">The identifier of the movie.</param>
        /// <returns>The movie, or <c>null</c> if it does not exist or the request failed.</returns>
        public async Task<DtoMovie?> RequestMovieAsync(long id)
        {
            try
            {
                return await HttpGetAsync<DtoMovie>($"api/items/movie/{id}");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Requests a TV show.
        /// </summary>
        /// <param name="id">The identifier of the TV show.</param>
        /// <returns>The TV show.</returns>
        public async Task<DtoMediaEntry> RequestTVShowAsync(long id)
        {
            return await HttpGetAsync<DtoTVShow>($"api/items/tvshow/{id}");
        }

        /// <summary>
        /// Requests a single TV show episode.
        /// </summary>
        /// <param name="id">The identifier of the episode.</param>
        /// <returns>The episode, or <c>null</c> if it does not exist or the request failed.</returns>
        public async Task<DtoTVShowEpisode?> RequestTVShowEpisodeAsync(long id)
        {
            try
            {
                return await HttpGetAsync<DtoTVShowEpisode>($"api/items/tvshowepisode/{id}");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Requests a page of media entries across all media sources, optionally filtered by search text.
        /// </summary>
        /// <param name="search">An optional search text to filter entries by name.</param>
        /// <param name="page">The zero-based page number to request.</param>
        /// <param name="size">The number of entries per page.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>The requested page of media entries.</returns>
        public Task<List<MediaEntryDto>> RequestItemsAsync(string? search = null, int page = 0, int size = 30, CancellationToken cancellationToken = default)
            => RequestItemsCoreAsync(null, page, size, search, 0, cancellationToken, includeIndividualMediaTypes: true);

        /// <summary>
        /// Requests the available genre options.
        /// </summary>
        /// <returns>The genre options, or an empty list if the request failed.</returns>
        public async Task<List<DtoGenreOption>> RequestGenreOptionsAsync()
        {
            try
            {
                return await HttpGetAsync<List<DtoGenreOption>>("api/items/genres");
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Saves updated metadata for a media entry.
        /// </summary>
        /// <param name="request">The metadata update request.</param>
        public async Task SaveMetadataAsync(MediaMetadataUpdateRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            _ = await HttpPostAsync<bool>(
                "api/items/metadata",
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        }

        #endregion

        #region Continue Watching
        /// <summary>
        /// Gets the current user's continue-watching list.
        /// </summary>
        /// <returns>The continue-watching entries.</returns>
        public async Task<List<ContinueWatchingDto>> GetContinueWatchingAsync()
        {
            return await HttpGetAsync<List<ContinueWatchingDto>>("api/continue-watching");
        }

        /// <summary>
        /// Reports playback progress for the current user.
        /// Coalescing: Für ein MediaItem läuft nur ein Request gleichzeitig.
        /// Währenddessen wird genau eine letzte Meldung gepuffert (überschreibend).
        /// </summary>
        /// <param name="mediaType">The type of the media entry (e.g. movie or episode).</param>
        /// <param name="mediaId">The identifier of the media entry.</param>
        /// <param name="positionSeconds">The current playback position, in seconds.</param>
        /// <param name="durationSeconds">The total duration of the media entry, in seconds.</param>
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
            var endPoint = "api/continue-watching/progress";
            async Task<HttpResponseMessage> DoRequestAsync() => await httpClient.PostAsync(endPoint, content);
            var response = await DoRequestAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Logger?.LogWarning("Received 401 Unauthorized from continue-watching/progress. Token might be expired.");
                if (await HandleUnauthorized())
                {
                    response = await DoRequestAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // Wenn nach Erneuerung weiterhin Unauthorized kommt, gib das weiter.
                        Logger?.LogWarning("Retry after token refresh still returned 401 for {EndPoint}.", endPoint);
                        throw new HttpRequestException($"Unauthorized: {endPoint}");
                    }
                }
                else
                {
                    // Kein neuer Token innerhalb der Wartezeit
                    throw new HttpRequestException($"Unauthorized: {endPoint}");
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to report progress: {response.ReasonPhrase}");
            }
        }
        /// <summary>
        /// Fetches the picture data for a given picture ID.
        /// </summary>
        /// <param name="pictureId">The ID of the picture to fetch.</param>
        /// <returns>A byte array containing the picture data, or an empty array if the picture could not be fetched.</returns>
        public async Task<byte[]> GetPictureAsync(long pictureId)
        {
            try
            {
                var response = await httpClient.GetAsync($"api/pictures/{pictureId}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error fetching picture with ID {PictureId}", pictureId);
            }
            return Array.Empty<byte>();
        }
        /// <summary>
        /// Fetches the source picture data for a given picture ID.
        /// </summary>
        /// <param name="pictureId">The ID of the source picture to fetch.</param>
        /// <returns>A byte array containing the source picture data, or an empty array if the picture could not be fetched.</returns>
        public async Task<byte[]> GetSourcePictureAsync(long pictureId)
        {
            try
            {
                var iconUrl = $"api/sourceicons/{pictureId}";
                var response = await httpClient.GetAsync(iconUrl);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error fetching source picture with ID {PictureId}", pictureId);
            }
            return Array.Empty<byte>();
        }
        /// <summary>
        /// Deletes a media source via the admin API.
        /// </summary>
        /// <param name="sourceId">The source identifier.</param>
        public async Task DeleteSourceAsync(long sourceId)
        {
            var endPoint = $"api/admin/sources/{sourceId}";
            var response = await httpClient.DeleteAsync(endPoint);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to delete source {sourceId}: {content}");
            }
        }

        #endregion
    }
}
