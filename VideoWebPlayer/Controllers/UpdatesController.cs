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
    [HttpPost("check")]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        await _antiforgery.ValidateRequestAsync(HttpContext);
        _logger.LogInformation("Manual update check requested.");

        var result = await _updates.CheckAsync(cancellationToken);
        return RedirectWithResult(result);
    }

    /// <summary>
    /// Triggers installation of a known update.
    /// </summary>
    [HttpPost("install")]
    public async Task<IActionResult> Install(CancellationToken cancellationToken)
    {
        await _antiforgery.ValidateRequestAsync(HttpContext);
        _logger.LogInformation("Manual update installation requested.");

        var result = await _updates.InstallAsync(cancellationToken);
        return RedirectWithResult(result);
    }

    private static RedirectResult RedirectWithResult(UpdateAdminActionResult result)
    {
        var key = result.Succeeded ? "updateStatus" : "updateError";
        return new RedirectResult($"/admin/updates?{key}={Uri.EscapeDataString(result.Message)}");
    }
}
