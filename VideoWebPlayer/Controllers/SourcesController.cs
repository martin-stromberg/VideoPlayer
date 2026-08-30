using VideoWebPlayer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

/// <summary>
/// Provides endpoints for managing media sources.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class SourcesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IUnlockedMediaService _unlockedMediaService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourcesController"/> class.
    /// </summary>
    /// <param name="authService">Authentication service.</param>
    /// <param name="db">Database context.</param>
    /// <param name="logger">Logger instance.</param>
    public SourcesController(IAuthService authService, ApplicationDbContext db, IUnlockedMediaService unlockedMediaService, ILogger<SourcesController> logger)
        :base(authService, logger)
    {
        _db = db;
        _unlockedMediaService = unlockedMediaService;
    }

    /// <summary>
    /// Gets the media sources for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSources()
    {
        try
        {
            CheckLogedIn();

            // Hole alle Quellen, die f�r den Benutzer freigeschaltet sind
            var sourceIds = await _db.MediaSourceUsers
                .AsNoTracking()
                .Where(msu => msu.UserId == CurrentUser.Id)
                .Select(msu => msu.MediaSourceId)
                .ToListAsync();

            var unlockedSourceIds = await _unlockedMediaService.GetUnlockedSourceIdsForUserAsync(CurrentUser.Id);
            sourceIds = sourceIds.Union(unlockedSourceIds).ToList();

            var sources = (await _db.MediaSources
                .AsNoTracking()
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

    /// <summary>
    /// Gets a specific media source by identifier.
    /// </summary>
    /// <param name="id">The source identifier.</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetSource(long id)
    {
        try
        {
            CheckLogedIn();
            var isSourceAllowed = await _db.MediaSourceUsers
                .AsNoTracking()
                .AnyAsync(msu => msu.UserId == CurrentUser.Id && msu.MediaSourceId == id);
            var unlockedSourceIds = await _unlockedMediaService.GetUnlockedSourceIdsForUserAsync(CurrentUser.Id);
            var hasUnlockedEntries = unlockedSourceIds.Contains(id);
            if (!isSourceAllowed && !hasUnlockedEntries)
                return Forbid("Kein Zugriff auf diese Quelle.");

            var source = await _db.MediaSources
                .AsNoTracking()
                .FirstOrDefaultAsync(ms => ms.Id == id);
            if (source is null)
                return NotFound("Quelle nicht gefunden.");

            var dto = Create<DtoMediaSource>(source);
            return Ok(dto);
        }
        catch (UnauthorizedAccessException ex)
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
