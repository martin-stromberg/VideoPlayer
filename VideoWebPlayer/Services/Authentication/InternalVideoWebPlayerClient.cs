using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VideoWebPlayer.Client;
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
                await ImpersonateAsync(user ?? httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal());
        }

        /// <summary>
        /// Issues an authenticated GET request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The response payload type.</typeparam>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <returns>The deserialized response.</returns>
        protected override async Task<T> HttpGetAsync<T>(string endPoint)
        {
            if (string.IsNullOrWhiteSpace(AuthorizationToken))
                await ImpersonateAsync(httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal());
            return await base.HttpGetAsync<T>(endPoint);
        }
        /// <summary>
        /// Issues an authenticated POST request to the specified endpoint.
        /// </summary>
        /// <typeparam name="T">The response payload type.</typeparam>
        /// <param name="endPoint">The endpoint to call.</param>
        /// <param name="args">The HTTP content payload.</param>
        /// <returns>The deserialized response.</returns>
        protected override async Task<T> HttpPostAsync<T>(string endPoint, HttpContent args, bool skipReauthorize = false)
        {
            if (string.IsNullOrWhiteSpace(AuthorizationToken))
                await ImpersonateAsync(httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal());
            return await base.HttpPostAsync<T>(endPoint, args, skipReauthorize);
        }

        /// <summary>
        /// Issues an authenticated POST request to the specified endpoint (non-generic).
        /// </summary>
        protected override async Task HttpPostAsync(string endPoint, HttpContent args, bool skipReauthorize = false)
        {
            if (string.IsNullOrWhiteSpace(AuthorizationToken))
                await ImpersonateAsync(httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal());
            await base.HttpPostAsync(endPoint, args, skipReauthorize);
        }

        private async Task ImpersonateAsync(ClaimsPrincipal user)
        {
            while (_impersonating)
                await Task.Delay(10);
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
                            throw new InvalidOperationException($"Could not resolve user from principal. Claims: {string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}"))}");

                        var token = authtorizationTokenService.CreateToken(currentUser);
                        base.SetAuthorizationToken(token);
                    }
                }
                catch (Exception ex)
                {
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
                    await ImpersonateAsync(user);
            }
        }
    }
}
