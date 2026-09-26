using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
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
    private readonly IPlaylistService _playlistService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSourcesController"/> class.
    /// </summary>
    /// <param name="authService">Authentication service.</param>
    /// <param name="db">Application database context.</param>
    /// <param name="playlistService">
    /// Used to resolve playlist-bound continue-watching replacements for the source's titles before they
    /// are deleted (see <see cref="DeleteSource"/>).
    /// </param>
    /// <param name="logger">Logger instance.</param>
    public AdminSourcesController(IAuthService authService, ApplicationDbContext db, IPlaylistService playlistService, ILogger<AdminSourcesController> logger)
        : base(authService, logger)
    {
        _db = db;
        _playlistService = playlistService;
    }

    /// <summary>
    /// Deletes a media source and all dependent data.
    /// </summary>
    /// <param name="id">The source identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="NoContentResult"/> on success, or a matching error result if the source does not exist or
    /// the caller is not authorized to delete it.
    /// </returns>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteSource(long id, CancellationToken cancellationToken)
    {
        try
        {
            if (!User.Identity?.IsAuthenticated == true)
                return Unauthorized("Benutzer ist nicht authentifiziert.");

            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid("Nur Administratoren dürfen Quellen löschen.");

            var source = await _db.MediaSources.FindAsync(new object[] { id }, cancellationToken);
            if (source is null)
                return NotFound("Quelle nicht gefunden.");

            // Before the affected ContinueWatchingEntries are unconditionally deleted, try to re-point any
            // playlist-bound one at another, still-existing title of the same playlist instead (see
            // IPlaylistService.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync). Runs
            // inside DeleteMediaSourceAsync's own transaction, so it is rolled back together with the
            // deletion should that fail.
            await _db.DeleteMediaSourceAsync(source, null, cancellationToken,
                ct => _playlistService.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(id, ct));
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
