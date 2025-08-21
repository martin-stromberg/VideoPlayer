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

[ApiController]
[Route("api/[controller]")]
[ApiTokenCheck()]
public class AuthController : ApiBaseController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;

    public AuthController(IAuthService authService, ApplicationDbContext db, ILogger<AuthController> logger)
        :base(authService, logger)
    {        
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthenticationRequest request)
    {
        try
        {
            return Ok(base.LoginAsync(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    
    [HttpPost("impersonate")]
    [BearerTokenCheck()]
    public async Task<IActionResult> Impersonate([FromBody] ImpersonateRequest request)
    {
        try
        {
            return Ok(base.Impersonate(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
}



public class ImpersonateRequest
{
    public string Email { get; set; } = "";
}