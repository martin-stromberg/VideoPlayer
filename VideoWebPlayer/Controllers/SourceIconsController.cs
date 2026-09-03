using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Provides endpoints for retrieving uploaded media source icons.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class SourceIconsController : ApiBaseController
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceIconsController"/> class.
    /// </summary>
    public SourceIconsController(ApplicationDbContext db, IAuthService authService, ILogger<SourceIconsController> logger)
        : base(authService, logger)
    {
        _db = db;
    }

    /// <summary>
    /// Gets a source icon by identifier.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        try
        {
            CheckLogedIn();

            // Ensure user is allowed to access the source that owns this icon
            var sourceId = await _db.MediaSourceUsers
                .AsNoTracking()
                .Where(msu => msu.UserId == CurrentUser.Id)
                .Join(_db.MediaSources.AsNoTracking(), msu => msu.MediaSourceId, ms => ms.Id, (_, ms) => ms)
                .Where(ms => ms.IconPictureId == id)
                .Select(ms => (long?)ms.Id)
                .FirstOrDefaultAsync();

            if (!sourceId.HasValue)
                return Forbid("Kein Zugriff auf dieses Icon.");

            var icon = await _db.MediaSourceIcons.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (icon == null || icon.Data.Length == 0)
                return NotFound();

            return File(icon.Data, icon.ContentType ?? "image/png");
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen des Source-Icons");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen des Source-Icons");
            return StatusCode(500, "Internal server error");
        }
    }
}
