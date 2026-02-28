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
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Client.Models;

/// <summary>
/// Provides authentication and impersonation endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiTokenCheck()]
public class AuthController : ApiBaseController
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="authService">Authentication service.</param>
    /// <param name="db">Application database context.</param>
    /// <param name="logger">Logger instance.</param>
    public AuthController(IAuthService authService, ApplicationDbContext db, ILogger<AuthController> logger)
        :base(authService, logger)
    {        
        _db = db;
    }

    /// <summary>
    /// Authenticates a user and returns an authorization token.
    /// </summary>
    /// <param name="request">The authentication request.</param>
    /// <returns>The authentication result.</returns>
    [HttpPost("login")]
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
    [BearerTokenCheck()]
    public async Task<IActionResult> Impersonate([FromBody] ImpersonateRequest request)
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