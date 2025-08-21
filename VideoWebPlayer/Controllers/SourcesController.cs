using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class SourcesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;

    public SourcesController(IAuthService authService, ApplicationDbContext db, ILogger<SourcesController> logger)
        :base(authService, logger)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetSources()
    {
        try
        {
            CheckLogedIn();

            // Hole alle Quellen, die für den Benutzer freigeschaltet sind
            var sourceIds = await _db.MediaSourceUsers
                .Where(msu => msu.UserId == CurrentUser.Id)
                .Select(msu => msu.MediaSourceId)
                .ToListAsync();

            var sources = (await _db.MediaSources
                .Where(ms => sourceIds.Contains(ms.Id))
                .ToListAsync())
                .Select(ms => Create<DtoMediaSource>(ms))
                .ToList();

            return Ok(sources);
        }
        catch(UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Quellen");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Quellen");
            return StatusCode(500, "Internal server error");
        }
    }
}
