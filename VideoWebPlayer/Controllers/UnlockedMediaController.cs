using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Provides endpoints for managing unlocked media entries.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class UnlockedMediaController : ApiBaseController
{
    private readonly IUnlockedMediaService _unlockedMediaService;
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnlockedMediaController"/> class.
    /// </summary>
    public UnlockedMediaController(IUnlockedMediaService unlockedMediaService, ApplicationDbContext db, IAuthService authService, ILogger<UnlockedMediaController> logger)
        : base(authService, logger)
    {
        _unlockedMediaService = unlockedMediaService;
        _db = db;
    }

    /// <summary>
    /// Gets the ids of users the given media entry is unlocked for. Only administrators are allowed.
    /// </summary>
    /// <param name="entry">The media entry.</param>
    /// <returns>Array of user ids.</returns>
    [HttpPost("users")]
    public async Task<IActionResult> GetUnlockedUsers([FromBody] DtoMediaEntry entry)
    {
        try
        {
            CheckLogedIn();
            if (!User.HasClaim("IsAdmin", "True"))
                return Unauthorized("Nur Administratoren duerfen Freischaltungen verwalten.");

            var userIds = await _unlockedMediaService.GetUnlockedUserIdsAsync(entry, HttpContext.RequestAborted);
            return Ok(userIds);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der freigeschalteten Benutzer");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Sets the users the given media entry is unlocked for. Only administrators are allowed.
    /// </summary>
    /// <param name="request">The request containing the media entry and target user ids.</param>
    /// <returns>HTTP 200 on success.</returns>
    [HttpPost("set")]
    public async Task<IActionResult> SetUnlockedUsers([FromBody] UnlockedUsersRequest request)
    {
        try
        {
            CheckLogedIn();
            if (!User.HasClaim("IsAdmin", "True"))
                return Unauthorized("Nur Administratoren duerfen Freischaltungen verwalten.");

            await _unlockedMediaService.SetUnlockedUsersAsync(request.Entry, request.UserIds, HttpContext.RequestAborted);
            return Ok(request.UserIds);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Ungueltige Freischalt-Anforderung");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Setzen der Freischaltungen");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets the list of all users for an administrator to select from.
    /// </summary>
    /// <returns>List of users with id and user name.</returns>
    [HttpGet("all-users")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            CheckLogedIn();
            if (!User.HasClaim("IsAdmin", "True"))
                return Unauthorized("Nur Administratoren duerfen Benutzer abrufen.");

            var users = await _db.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.UserName })
                .ToArrayAsync(HttpContext.RequestAborted);

            return Ok(users);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Benutzerliste");
            return StatusCode(500, "Internal server error");
        }
    }
}
