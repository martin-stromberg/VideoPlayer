using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using VideoWebPlayer.Client;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Authentication
{
    public class InternalVideoWebPlayerClient : VideoWebPlayerClient
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly AuthorizationTokenService authtorizationTokenService;
        private bool _impersonating;

        public InternalVideoWebPlayerClient(
            IHttpContextAccessor httpContextAccessor,
            HttpClient httpClient, 
            UserManager<ApplicationUser> userManager, 
            AuthorizationTokenService authtorizationTokenService, 
            ILogger<VideoWebPlayerClient> logger) : base(httpClient, logger)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.userManager = userManager;
            this.authtorizationTokenService = authtorizationTokenService;
        }
        protected override async Task<T> HttpGetAsync<T>(string endPoint)
        {
            if (string.IsNullOrWhiteSpace(AuthorizationToken))
                await ImpersonateAsync(httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal());
            return await base.HttpGetAsync<T>(endPoint);
        }
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
