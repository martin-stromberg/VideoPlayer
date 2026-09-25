using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VideoWebPlayer.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Client.Models;

/// <summary>
/// Provides authentication and impersonation endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthorizationTokenService _authorizationTokenService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="authService">Authentication service.</param>
    /// <param name="db">Application database context.</param>
    /// <param name="refreshTokenService">Refresh token service.</param>
    /// <param name="userManager">User manager for identity lookups.</param>
    /// <param name="authorizationTokenService">JWT token service.</param>
    /// <param name="logger">Logger instance.</param>
    public AuthController(
        IAuthService authService,
        ApplicationDbContext db,
        IRefreshTokenService refreshTokenService,
        UserManager<ApplicationUser> userManager,
        AuthorizationTokenService authorizationTokenService,
        ILogger<AuthController> logger)
        : base(authService, logger)
    {
        _db = db;
        _refreshTokenService = refreshTokenService;
        _userManager = userManager;
        _authorizationTokenService = authorizationTokenService;
    }

    /// <summary>
    /// Authenticates a user and returns an authorization token.
    /// </summary>
    /// <param name="request">The authentication request.</param>
    /// <returns>The authentication result.</returns>
    [HttpPost("login")]
    [ApiTokenCheck(ApiTokenScope.MauiOnly)]
    public async Task<IActionResult> Login([FromBody] AuthenticationRequest request)
    {
        try
        {
            return Ok(await base.LoginAsync(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }


    /// <summary>
    /// Issues an impersonation token for an administrator request.
    /// </summary>
    /// <param name="request">The impersonation request.</param>
    /// <returns>The authentication result.</returns>
    [HttpPost("impersonate")]
    [ApiTokenCheck()]
    [BearerTokenCheck()]
    public new async Task<IActionResult> Impersonate([FromBody] ImpersonateRequest request)
    {
        try
        {
            return Ok(await base.Impersonate(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    /// <summary>
    /// Rotates a refresh token and returns a new JWT access token plus the rotated refresh token.
    /// </summary>
    /// <param name="request">The refresh request.</param>
    /// <returns>The refreshed session.</returns>
    [HttpPost("refresh")]
    [ApiTokenCheck(ApiTokenScope.MauiOnly)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("Ungültiger Refresh-Request.");

        var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken);
        if (!rotation.Success || rotation.UserId == null || rotation.NewToken == null)
            return Unauthorized("Ungültiger oder abgelaufener Refresh-Token.");

        var user = await _userManager.FindByIdAsync(rotation.UserId);
        if (user == null)
            return Unauthorized("Ungültiger oder abgelaufener Refresh-Token.");

        var token = _authorizationTokenService.CreateToken(user);
        return Ok(new RefreshTokenResponse
        {
            Token = token.token,
            Expires = token.expires,
            RefreshToken = rotation.NewToken
        });
    }

    /// <summary>
    /// Revokes a refresh token (logout on the device). Idempotent — always returns 200.
    /// </summary>
    /// <param name="request">The logout request.</param>
    /// <returns>An empty success result.</returns>
    [HttpPost("logout")]
    [ApiTokenCheck(ApiTokenScope.MauiOnly)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        if (request?.RefreshToken != null)
            await _refreshTokenService.RevokeAsync(request.RefreshToken);

        return Ok();
    }
}



/// <summary>
/// Request payload for impersonation.
/// </summary>
public class ImpersonateRequest
{
    /// <summary>
    /// Gets or sets the email to impersonate.
    /// </summary>
    public string Email { get; set; } = "";
}
