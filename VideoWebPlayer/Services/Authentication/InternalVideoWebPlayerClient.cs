using Microsoft.AspNetCore.Identity;
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
        protected override async Task<T> HttpPostAsync<T>(string endPoint, HttpContent args)
        {
            if (string.IsNullOrWhiteSpace(AuthorizationToken))
                await ImpersonateAsync(httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal());
            return await base.HttpPostAsync<T>(endPoint, args);
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
                        var token = authtorizationTokenService.CreateToken(currentUser);
                        base.SetAuthorizationToken(token);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Could not impersonate current user.");
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
