using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Authentication
{
    public interface IAuthService
    {
        public ApplicationUser? CurrentUser { get; }

        Task<AuthorizationToken> ImpersonateAsync(ImpersonateRequest request);
        Task<AuthorizationToken> LoginAsync(AuthenticationRequest request);
    }

    public class AuthorizationTokenService
    {
        private readonly SymmetricSecurityKey _jwtKey;
        private readonly IConfiguration _config;

        public AuthorizationTokenService(SymmetricSecurityKey jwtKey, IConfiguration config)
        {
            this._jwtKey = jwtKey;
            _config = config;
        }

        public AuthorizationToken CreateToken(ApplicationUser user)
        {
            if (user is null) return null;
            // JWT generieren
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
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

    public class AuthService : IAuthService
    {
        private readonly AuthorizationTokenService authtorizationTokenService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthService> _logger;

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


        public ApplicationUser? CurrentUser
        {
            get
            {                
                return _userManager.GetUserAsync(_httpContextAccessor?.HttpContext?.User).Result;
            }
        }
        protected bool IsAdmin
        {
            get
            {
                return CurrentUser is not null && CurrentUser.IsAdmin;
            }
        }

        protected void CheckAdmin()
        {
            if (!IsAdmin)
                throw new UnauthorizedAccessException("Nur Administratoren dürfen diesen Funktion nutzen.");
        }

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
