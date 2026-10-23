using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Provides the program guide ("Programmauskunft") of the automated TV channel to signed-in users:
/// the "now/next" answer and a bounded preview of the coming hours. The full schedule remains an
/// administrative view (<see cref="Components.Pages.Admin.TVSenderAdmin"/>).
/// </summary>
[ApiController]
[Route("api/program-guide")]
[BearerTokenCheck]
public class ProgramGuideController : ApiBaseController
{
    private const int DefaultPreviewHours = 6;

    private readonly IProgramGuideService _programGuideService;
    private readonly ProgramSettingsService _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgramGuideController"/> class.
    /// </summary>
    /// <param name="programGuideService">Program guide service.</param>
    /// <param name="settings">Program settings holding the TV-channel release switch.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public ProgramGuideController(IProgramGuideService programGuideService, ProgramSettingsService settings, IAuthService authService, ILogger<ProgramGuideController> logger)
        : base(authService, logger)
    {
        _programGuideService = programGuideService;
        _settings = settings;
    }

    // The whole TV-channel feature sits behind an admin release switch: while unreleased the
    // endpoints answer 404 — the feature is indistinguishable from "does not exist".
    private async Task<bool> FeatureReleasedAsync()
        => await _settings.GetTvSenderEnabledAsync(HttpContext.RequestAborted);

    // N28: zusätzlich zur globalen Freigabe muss der aufrufende Benutzer freigegeben sein —
    // sonst 403 (sichtbar als „kein Zugriff", nicht als „Feature existiert nicht").
    // Administratoren sind implizit freigegeben.
    private async Task<bool> AccessGrantedAsync()
        => await _settings.IsTvSenderAccessGrantedAsync(
            CurrentUser?.Id, CurrentUser?.IsAdmin ?? false, HttpContext.RequestAborted);

    /// <summary>
    /// Gets which broadcast runs right now (with its position inside the video and its remaining
    /// duration) and which one follows — purely time-based, independent of viewers.
    /// </summary>
    /// <returns>The "now/next" answer as <see cref="DtoProgramGuideNow"/>, or an error result.</returns>
    [HttpGet("now")]
    public Task<IActionResult> GetNow()
    {
        return ExecuteAsync(async () =>
        {
            CheckLoggedIn();
            if (!await FeatureReleasedAsync())
                return NotFound();
            if (!await AccessGrantedAsync())
                return Forbid();
            var result = await _programGuideService.GetNowAsync(DateTimeOffset.UtcNow, HttpContext.RequestAborted);
            return Ok(result);
        }, "Abrufen der Programmauskunft (Jetzt/Gleich)");
    }

    /// <summary>
    /// Gets the bounded program overview of the coming hours: the running broadcast plus the
    /// scheduled broadcasts starting within the preview window. The window is capped server-side
    /// by <see cref="ProgramGuideService.MaxPreviewHours"/> — the full schedule stays administrative.
    /// </summary>
    /// <param name="hours">How many hours ahead to look (default 6, maximum
    /// <see cref="ProgramGuideService.MaxPreviewHours"/>).</param>
    /// <returns>The program entries as <see cref="DtoProgramGuideEntry"/> array, or an error result.</returns>
    [HttpGet("upcoming")]
    public Task<IActionResult> GetUpcoming([FromQuery] int hours = DefaultPreviewHours)
    {
        return ExecuteAsync(async () =>
        {
            CheckLoggedIn();
            if (!await FeatureReleasedAsync())
                return NotFound();
            if (!await AccessGrantedAsync())
                return Forbid();
            // Range check before TimeSpan.FromHours: extreme values would overflow into a 500.
            if (hours < 1 || hours > ProgramGuideService.MaxPreviewHours)
                return BadRequest($"hours muss zwischen 1 und {ProgramGuideService.MaxPreviewHours} liegen.");

            var result = await _programGuideService.GetUpcomingAsync(
                DateTimeOffset.UtcNow, TimeSpan.FromHours(hours), HttpContext.RequestAborted);
            return Ok(result);
        }, "Abrufen der Programmvorschau");
    }

    /// <summary>
    /// Runs <paramref name="action"/> and maps the exceptions common to both endpoints to the
    /// corresponding HTTP response — same pattern as <see cref="PlaylistsController"/>.
    /// </summary>
    /// <param name="action">The endpoint logic to run.</param>
    /// <param name="logContext">A German gerund phrase describing the action, used in log messages.</param>
    /// <returns>The result produced by <paramref name="action"/>, or the mapped error response.</returns>
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action, string logContext)
    {
        try
        {
            return await action();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim {LogContext}", logContext);
            return Unauthorized(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Logger.LogWarning(ex, "Ungueltige Vorschaudauer beim {LogContext}", logContext);
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Ungueltige Eingabe beim {LogContext}", logContext);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim {LogContext}", logContext);
            return StatusCode(500, "Internal server error");
        }
    }
}
