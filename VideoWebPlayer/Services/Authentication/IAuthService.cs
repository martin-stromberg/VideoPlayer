using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Authentication
{
    /// <summary>
    /// Provides authentication operations and access to the current user.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Gets the current authenticated user, if available.
        /// </summary>
        public ApplicationUser? CurrentUser { get; }

        /// <summary>
        /// Creates an impersonation token for the specified request.
        /// </summary>
        /// <param name="request">The impersonation request.</param>
        /// <returns>The issued authorization token.</returns>
        Task<AuthorizationToken> ImpersonateAsync(ImpersonateRequest request);
        /// <summary>
        /// Logs in a user and returns an authorization token.
        /// </summary>
        /// <param name="request">The authentication request.</param>
        /// <returns>The issued authorization token.</returns>
        Task<AuthorizationToken> LoginAsync(AuthenticationRequest request);
    }

    /// <summary>
    /// Creates JWT authorization tokens for authenticated users.
    /// </summary>
    public class AuthorizationTokenService
    {
        private readonly SymmetricSecurityKey _jwtKey;
        private readonly IConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationTokenService"/> class.
        /// </summary>
        /// <param name="jwtKey">The symmetric security key for signing tokens.</param>
        /// <param name="config">The configuration provider.</param>
        public AuthorizationTokenService(SymmetricSecurityKey jwtKey, IConfiguration config)
        {
            this._jwtKey = jwtKey;
            _config = config;
        }

        /// <summary>
        /// Creates a signed authorization token for the specified user.
        /// </summary>
        /// <param name="user">The user to create a token for.</param>
        /// <returns>The generated authorization token, or <c>null</c> when user is null.</returns>
        public AuthorizationToken CreateToken(ApplicationUser user)
        {
            if (user is null) return null;
            // JWT generieren
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

            var creds = new SigningCredentials(_jwtKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Issuer"],
                claims,
                expires: DateTime.Now.AddHours(12),
                signingCredentials: creds);

            return new AuthorizationToken()
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expires = token.ValidTo
            };
        }
    }

    /// <summary>
    /// Implements authentication and impersonation workflows.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly AuthorizationTokenService authtorizationTokenService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthService"/> class.
        /// </summary>
        /// <param name="authtorizationTokenService">Token service used to issue JWTs.</param>
        /// <param name="userManager">User manager for identity lookups.</param>
        /// <param name="signInManager">Sign-in manager for credential validation.</param>
        /// <param name="configuration">Configuration provider.</param>
        /// <param name="httpContextAccessor">HTTP context accessor.</param>
        /// <param name="logger">Logger instance.</param>
        public AuthService(
            AuthorizationTokenService authtorizationTokenService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor, 
            ILogger<AuthService> logger)
        {
            this.authtorizationTokenService = authtorizationTokenService;
            this._userManager = userManager;
            this._signInManager = signInManager;
            this._httpContextAccessor = httpContextAccessor;
            this._logger = logger;
        }


        /// <summary>
        /// Gets the current authenticated user from the HTTP context.
        /// </summary>
        public ApplicationUser? CurrentUser
        {
            get
            {                
                return _userManager.GetUserAsync(_httpContextAccessor?.HttpContext?.User).Result;
            }
        }
        /// <summary>
        /// Gets a value indicating whether the current user is an administrator.
        /// </summary>
        protected bool IsAdmin
        {
            get
            {
                return CurrentUser is not null && CurrentUser.IsAdmin;
            }
        }

        /// <summary>
        /// Ensures the current user has administrator privileges.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Thrown when the user is not an administrator.</exception>
        protected void CheckAdmin()
        {
            if (!IsAdmin)
                throw new UnauthorizedAccessException("Nur Administratoren dürfen diesen Funktion nutzen.");
        }

        /// <summary>
        /// Validates credentials and issues an authorization token.
        /// </summary>
        /// <param name="request">The authentication request.</param>
        /// <returns>The issued authorization token.</returns>
        public async Task<AuthorizationToken> LoginAsync(AuthenticationRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                throw new UnauthorizedAccessException("Benutzer nicht gefunden.");

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
                throw new UnauthorizedAccessException("Ungültige Zugangsdaten.");
            return authtorizationTokenService.CreateToken(user);
        }
        
        /// <summary>
        /// Issues an authorization token for the specified user as an admin-only operation.
        /// </summary>
        /// <param name="request">The impersonation request.</param>
        /// <returns>The issued authorization token.</returns>
        public async Task<AuthorizationToken> ImpersonateAsync(ImpersonateRequest request)
        {
            CheckAdmin();
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                throw new UnauthorizedAccessException("Benutzer nicht gefunden.");
            return authtorizationTokenService.CreateToken(user);
        }
    }
}
