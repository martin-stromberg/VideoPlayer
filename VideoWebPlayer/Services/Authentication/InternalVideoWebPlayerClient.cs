using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Authentication
{
    /// <summary>
    /// Internal client that injects an authorization token for the current HTTP user.
    /// </summary>
    public class InternalVideoWebPlayerClient : VideoWebPlayerClient
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly AuthorizationTokenService authtorizationTokenService;
        private bool _impersonating;

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalVideoWebPlayerClient"/> class.
        /// </summary>
        /// <param name="httpClient">The underlying HTTP client.</param>
        /// <param name="httpContextAccessor">HTTP context accessor.</param>
        /// <param name="userManager">User manager for identity lookups.</param>
        /// <param name="authtorizationTokenService">Token service used to issue JWTs.</param>
        /// <param name="logger">Logger instance.</param>
        public InternalVideoWebPlayerClient(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager,
            AuthorizationTokenService authtorizationTokenService,
            ILogger<VideoWebPlayerClient> logger) : base(httpClient, logger)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.userManager = userManager;
            this.authtorizationTokenService = authtorizationTokenService;
        }
        /// <summary>
        /// Ensures an authorization token is available for the given user.
        /// </summary>
        /// <param name="user">The user to impersonate. If null, the current HTTP context user is used.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public override async Task EnsureAuthorizationTokenAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(AuthorizationToken))
                await ImpersonateAsync(user ?? httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(), cancellationToken);
        }

        /// <summary>
        /// Obtains a bearer token for the current HTTP user before a request leaves this client, so every
        /// HTTP verb — not only GET and POST — runs under the signed-in user. Uses
        /// <see cref="EnsureAuthorizationTokenAsync(ClaimsPrincipal?, CancellationToken)"/> instead of
        /// repeating its check, so both paths stay in step.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the identity lookups of the impersonation.</param>
        /// <returns>A task that completes once a bearer token is available.</returns>
        private Task EnsureImpersonatedAsync(CancellationToken cancellationToken = default)
            => EnsureAuthorizationTokenAsync(null, cancellationToken);

        /// <summary>
        /// Issues an authenticated GET request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The response payload type.</typeparam>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <returns>The deserialized response.</returns>
        protected override async Task<T> HttpGetAsync<T>(string endPoint)
        {
            await EnsureImpersonatedAsync();
            return await base.HttpGetAsync<T>(endPoint);
        }

        /// <summary>
        /// Issues an authenticated GET request to the specified endpoint, observing a cancellation token.
        /// </summary>
        /// <typeparam name="T">The response payload type.</typeparam>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>The deserialized response.</returns>
        protected override async Task<T> HttpGetAsync<T>(string endPoint, CancellationToken cancellationToken)
        {
            await EnsureImpersonatedAsync(cancellationToken);
            return await base.HttpGetAsync<T>(endPoint, cancellationToken);
        }

        /// <summary>
        /// Issues an authenticated POST request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The response payload type.</typeparam>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <param name="args">The HTTP content payload.</param>
        /// <param name="skipReauthorize">Whether to skip the automatic re-authorization retry.</param>
        /// <returns>The deserialized response.</returns>
        protected override async Task<T> HttpPostAsync<T>(string endPoint, HttpContent args, bool skipReauthorize = false)
        {
            await EnsureImpersonatedAsync();
            return await base.HttpPostAsync<T>(endPoint, args, skipReauthorize);
        }

        /// <summary>
        /// Issues an authenticated POST request to the specified endpoint (non-generic).
        /// </summary>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <param name="args">The HTTP content payload.</param>
        /// <param name="skipReauthorize">Whether to skip the automatic re-authorization retry.</param>
        protected override async Task HttpPostAsync(string endPoint, HttpContent args, bool skipReauthorize = false)
        {
            await EnsureImpersonatedAsync();
            await base.HttpPostAsync(endPoint, args, skipReauthorize);
        }

        /// <summary>
        /// Issues an authenticated PUT request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The response payload type.</typeparam>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <param name="args">The HTTP content payload.</param>
        /// <returns>The deserialized response.</returns>
        protected override async Task<T> HttpPutAsync<T>(string endPoint, HttpContent args)
        {
            await EnsureImpersonatedAsync();
            return await base.HttpPutAsync<T>(endPoint, args);
        }

        /// <summary>
        /// Issues an authenticated PUT request to the specified endpoint (non-generic).
        /// </summary>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <param name="args">The HTTP content payload.</param>
        protected override async Task HttpPutAsync(string endPoint, HttpContent args)
        {
            await EnsureImpersonatedAsync();
            await base.HttpPutAsync(endPoint, args);
        }

        /// <summary>
        /// Issues an authenticated PATCH request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The response payload type.</typeparam>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <param name="args">The HTTP content payload.</param>
        /// <returns>The deserialized response.</returns>
        protected override async Task<T> HttpPatchAsync<T>(string endPoint, HttpContent args)
        {
            await EnsureImpersonatedAsync();
            return await base.HttpPatchAsync<T>(endPoint, args);
        }

        /// <summary>
        /// Issues an authenticated DELETE request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The response payload type.</typeparam>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <returns>The deserialized response.</returns>
        protected override async Task<T> HttpDeleteAsync<T>(string endPoint)
        {
            await EnsureImpersonatedAsync();
            return await base.HttpDeleteAsync<T>(endPoint);
        }

        /// <summary>
        /// Issues an authenticated DELETE request to the specified endpoint (non-generic).
        /// </summary>
        /// <param name="endPoint">The endpoint to call.</param>
        protected override async Task HttpDeleteAsync(string endPoint)
        {
            await EnsureImpersonatedAsync();
            await base.HttpDeleteAsync(endPoint);
        }

        /// <summary>
        /// Issues an authenticated POST request for the playlist navigation endpoints
        /// (<c>play/next</c>, <c>play/previous</c>, <c>play/advance</c>), which answer either a
        /// navigation result or 204 No Content.
        /// </summary>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <returns>The deserialized navigation result, or <c>null</c> on 204 No Content.</returns>
        protected override async Task<DtoPlaylistNavigationResult?> PostForOptionalPlaylistNavigationResultAsync(string endPoint)
        {
            await EnsureImpersonatedAsync();
            return await base.PostForOptionalPlaylistNavigationResultAsync(endPoint);
        }

        /// <summary>
        /// Resolves the user behind <paramref name="user"/> and puts a bearer token for them on this
        /// client. The cancellation token ends the waiting for a parallel impersonation, so a request
        /// the browser has already abandoned does not keep waiting here.
        /// </summary>
        /// <param name="user">The principal to resolve the user from.</param>
        /// <param name="cancellationToken">A token to cancel the impersonation.</param>
        /// <returns>A task that completes once a bearer token is available.</returns>
        private async Task ImpersonateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            while (_impersonating)
                await Task.Delay(10, cancellationToken);
            var repeat = false;
            try
            {
                lock (userManager)
                {
                    if (_impersonating)
                    {
                        repeat = true;
                        return;
                    }
                    _impersonating = true;
                }
                try
                {
                    if (string.IsNullOrWhiteSpace(AuthorizationToken))
                    {
                        var currentUser = await userManager.GetUserAsync(user);
                        if (currentUser is null)
                        {
                            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
                            if (!string.IsNullOrWhiteSpace(userId))
                                currentUser = await userManager.FindByIdAsync(userId);
                        }
                        if (currentUser is null)
                        {
                            var email = user.FindFirstValue(ClaimTypes.Email);
                            if (!string.IsNullOrWhiteSpace(email))
                                currentUser = await userManager.FindByEmailAsync(email);
                        }
                        if (currentUser is null)
                        {
                            var name = user.FindFirstValue(ClaimTypes.Name);
                            if (!string.IsNullOrWhiteSpace(name))
                                currentUser = await userManager.FindByNameAsync(name);
                        }
                        if (currentUser is null)
                        {
                            if (user.Identity?.IsAuthenticated != true)
                            {
                                Logger.LogWarning("Could not impersonate anonymous request: no authenticated identity present.");
                                throw new UnauthorizedAccessException("Not authenticated.");
                            }
                            throw new InvalidOperationException($"Could not resolve user from principal. Claims: {string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}"))}");
                        }

                        var token = authtorizationTokenService.CreateToken(currentUser);
                        base.SetAuthorizationToken(token);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not UnauthorizedAccessException)
                        Logger.LogError(ex, "Could not impersonate current user.");
                    throw;
                }
                finally
                {
                    lock (userManager)
                        _impersonating = false;
                }
            }
            finally
            {
                if (repeat)
                    await ImpersonateAsync(user, cancellationToken);
            }
        }
    }
}
