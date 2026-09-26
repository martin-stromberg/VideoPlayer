using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<UpdatesController> _logger;

    /// <summary>
    /// Creates a new update controller.
    /// </summary>
    /// <param name="updates">The update administration service.</param>
    /// <param name="antiforgery">The antiforgery service used to validate requests.</param>
    /// <param name="logger">The logger.</param>
    public UpdatesController(
        UpdateAdminService updates,
        IAntiforgery antiforgery,
        ILogger<UpdatesController> logger)
    {
        _updates = updates;
        _antiforgery = antiforgery;
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
