using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Services.Updates;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Provides server-side update administration endpoints.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("admin/updates/api")]
public sealed class UpdatesController : ControllerBase
{
    private readonly UpdateAdminService _updates;
    private readonly IAntiforgery _antiforgery;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UpdatesController> _logger;

    /// <summary>
    /// Creates a new update controller.
    /// </summary>
    /// <param name="updates">The update administration service.</param>
    /// <param name="antiforgery">The antiforgery service used to validate requests.</param>
    /// <param name="scopeFactory">The scope factory used to run the installation in a fresh scope.</param>
    /// <param name="logger">The logger.</param>
    public UpdatesController(
        UpdateAdminService updates,
        IAntiforgery antiforgery,
        IServiceScopeFactory scopeFactory,
        ILogger<UpdatesController> logger)
    {
        _updates = updates;
        _antiforgery = antiforgery;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a manual update check.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A redirect to the update page carrying the result of the check.</returns>
    [HttpPost("check")]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        var antiforgery = await _antiforgery.IsRequestValidAsync(HttpContext);
        if (!antiforgery)
        {
            _logger.LogWarning("Missing or invalid antiforgery token for manual update check.");
            return RedirectWithCheckResult(UpdateAdminActionResult.Failed("Das Antiforgery-Token fehlt oder ist ungültig."));
        }

        _logger.LogInformation("Manual update check requested.");

        var result = await _updates.CheckAsync(cancellationToken);
        return RedirectWithCheckResult(result);
    }

    /// <summary>
    /// Triggers installation of a known update.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A redirect to the update page carrying the result of the installation.</returns>
    [HttpPost("install")]
    public async Task<IActionResult> Install(CancellationToken cancellationToken)
    {
        var antiforgery = await _antiforgery.IsRequestValidAsync(HttpContext);
        if (!antiforgery)
        {
            _logger.LogWarning("Missing or invalid antiforgery token for manual update installation.");
            return RedirectWithResult(UpdateAdminActionResult.Failed("Das Antiforgery-Token fehlt oder ist ungültig."));
        }

        _logger.LogInformation("Manual update installation requested.");

        var result = await _updates.InstallAsync(cancellationToken);
        return RedirectWithResult(result);
    }

    /// <summary>
    /// Starts the installation of a known update in the background and answers immediately.
    /// Called fire-and-forget by the static progress page (<c>update-progress.html</c>) before the
    /// update shuts the process down; the answer may therefore be lost without harm.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 when the installation was started; 409 while an action runs or no installable version is known.</returns>
    [HttpPost("~/api/updates/install")]
    public async Task<IActionResult> InstallFromProgressPage(CancellationToken cancellationToken)
    {
        // Die statische Fortschrittsseite kann kein Antiforgery-Token rendern. Stattdessen
        // verlangt der Endpunkt einen Header, den ein Cross-Site-Formular nicht setzen kann;
        // das SameSite=Lax-Auth-Cookie kommt ohnehin nicht aus fremden Kontexten mit.
        if (!string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal))
        {
            _logger.LogWarning("Update installation via progress endpoint rejected: missing X-Requested-With header.");
            return BadRequest(new { error = "Der Header X-Requested-With fehlt." });
        }

        var snapshot = await _updates.GetSnapshotAsync(cancellationToken);
        if (UpdateAdminService.IsBusy(snapshot.Status))
            return Conflict(new { error = "Es läuft bereits eine Update-Aktion." });
        if (!UpdateAdminService.IsInstallable(snapshot.Status))
            return Conflict(new { error = "Es ist keine installierbare Version bekannt." });

        _logger.LogInformation("Update installation requested from progress page.");
        _ = RunInstallInBackgroundAsync();
        return Accepted();
    }

    private async Task RunInstallInBackgroundAsync()
    {
        try
        {
            // Neuer Scope: Der Request-Scope ist nach der sofortigen Antwort bereits disposed.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var updates = scope.ServiceProvider.GetRequiredService<UpdateAdminService>();
            var result = await updates.InstallAsync(CancellationToken.None);
            if (result.Succeeded)
                _logger.LogInformation("Background update installation finished: {Message}", result.Message);
            else
                _logger.LogWarning("Background update installation did not succeed: {Message}", result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background update installation failed.");
        }
    }

    private static RedirectResult RedirectWithResult(UpdateAdminActionResult result)
    {
        var key = result.Succeeded ? "updateStatus" : "updateError";
        return new RedirectResult($"/admin/updates?{key}={Uri.EscapeDataString(result.Message)}");
    }

    private static RedirectResult RedirectWithCheckResult(UpdateAdminActionResult result)
    {
        var key = result.Succeeded ? "checkStatus" : "checkError";
        return new RedirectResult($"/admin/updates?{key}={Uri.EscapeDataString(result.Message)}");
    }
}
