using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Provides administrative endpoints for media sources.
/// </summary>
[ApiController]
[Route("api/admin/sources")]
[BearerTokenCheck]
public class AdminSourcesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSourcesController"/> class.
    /// </summary>
    /// <param name="authService">Authentication service.</param>
    /// <param name="db">Application database context.</param>
    /// <param name="logger">Logger instance.</param>
    public AdminSourcesController(IAuthService authService, ApplicationDbContext db, ILogger<AdminSourcesController> logger)
        : base(authService, logger)
    {
        _db = db;
    }

    /// <summary>
    /// Deletes a media source and all dependent data.
    /// </summary>
    /// <param name="id">The source identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteSource(long id, CancellationToken cancellationToken)
    {
        try
        {
            if (!User.Identity?.IsAuthenticated == true)
                return Unauthorized("Benutzer ist nicht authentifiziert.");

            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid("Nur Administratoren duerfen Quellen loeschen.");

            var source = await _db.MediaSources.FindAsync(new object[] { id }, cancellationToken);
            if (source is null)
                return NotFound("Quelle nicht gefunden.");

            await _db.DeleteMediaSourceAsync(source, null, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Loeschen der Quelle");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Loeschen der Quelle (Id={Id})", id);
            return StatusCode(500, ex.Message);
        }
    }
}
