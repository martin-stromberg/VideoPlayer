using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers
{
    /// <summary>
    /// Base controller providing shared authentication helpers.
    /// </summary>
    public class ApiBaseController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiBaseController"/> class.
        /// </summary>
        /// <param name="authService">Authentication service.</param>
        /// <param name="logger">Logger instance.</param>
        public ApiBaseController(IAuthService authService, ILogger logger)
            : base()
        {
            _authService = authService;
            _logger = logger;
        }
        /// <summary>
        /// Gets the logger instance.
        /// </summary>
        protected ILogger Logger => _logger;
        /// <summary>
        /// Gets the current authenticated user.
        /// </summary>
        protected ApplicationUser? CurrentUser => _authService.CurrentUser;

        /// <summary>
        /// Throws when no authenticated user is available.
        /// </summary>
        protected void CheckLoggedIn()
        {
            if (CurrentUser is null)
                throw new UnauthorizedAccessException("Sie sind nicht angemeldet.");
        }

        /// <summary>
        /// Legacy alias for <see cref="CheckLoggedIn"/>.
        /// </summary>
        protected void CheckLogedIn() => CheckLoggedIn();

        /// <summary>
        /// Loads the shared placeholder image from wwwroot, using the given cache to avoid repeated disk reads.
        /// </summary>
        /// <param name="cache">The memory cache instance used to cache the placeholder bytes.</param>
        /// <returns>The placeholder image bytes, or <c>null</c> if the placeholder file does not exist.</returns>
        protected static async Task<byte[]?> GetPlaceholderBytesAsync(IMemoryCache cache)
        {
            var placeholderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/placeholder.png");
            if (!System.IO.File.Exists(placeholderPath))
                return null;

            return await cache.GetOrCreateAsync("ApiBaseController.Placeholder", async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                return await System.IO.File.ReadAllBytesAsync(placeholderPath);
            });
        }

        /// <summary>
        /// Creates a DTO by copying matching properties from a source object.
        /// </summary>
        /// <typeparam name="T">The DTO type.</typeparam>
        /// <param name="ms">The source instance.</param>
        /// <returns>The populated DTO.</returns>
        protected T Create<T>(object ms)
        {
            var sourceType = ms.GetType();
            var record = Activator.CreateInstance<T>();
            foreach (var prop in typeof(T).GetProperties().Where(p => !p.GetCustomAttributes(typeof(IgnoreAssignPropertyAttribute), false).Any()))
            {
                var sourceProp = sourceType.GetProperty(prop.Name);
                if (sourceProp != null && sourceProp.CanRead)
                {
                    var value = sourceProp.GetValue(ms);
                    prop.SetValue(record, value);
                }
            }
            return record;
        }

        /// <summary>
        /// Logs in and returns an authorization token.
        /// </summary>
        /// <param name="request">The authentication request.</param>
        /// <returns>The authorization token.</returns>
        internal async Task<AuthorizationToken> LoginAsync(AuthenticationRequest request)
        {
            return await _authService.LoginAsync(request);
        }

        /// <summary>
        /// Impersonates a user and returns an authorization token.
        /// </summary>
        /// <param name="request">The impersonation request.</param>
        /// <returns>The authorization token.</returns>
        internal async Task<AuthorizationToken> Impersonate(ImpersonateRequest request)
        {
            return await _authService.ImpersonateAsync(request);
        }
    }
}
