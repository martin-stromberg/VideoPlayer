using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using msTools.Backup;
using System.Security.Claims;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Provides server-side backup endpoints.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("admin/backups/api")]
public sealed class BackupsController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly VideoWebPlayerBackupFacade _backupFacade;
    private readonly ILogger<BackupsController> _logger;

    /// <summary>
    /// Creates a new controller.
    /// </summary>
    public BackupsController(
        IBackupService backupService,
        VideoWebPlayerBackupFacade backupFacade,
        ILogger<BackupsController> logger)
    {
        _backupService = backupService;
        _backupFacade = backupFacade;
        _logger = logger;
    }

    /// <summary>
    /// Creates a manual backup from a regular server-side form post.
    /// </summary>
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation("Manual backup creation requested by user {UserId}.", userId);

        var result = await _backupFacade.CreateManualBackupAsync(userId, cancellationToken);
        var parameter = result.Succeeded ? "backupStatus" : "backupError";
        var message = result.Succeeded
            ? result.Message
            : string.Join(" ", result.Errors.DefaultIfEmpty(result.Message));

        return Redirect($"/admin/backups?{parameter}={Uri.EscapeDataString(message)}");
    }

    /// <summary>
    /// Downloads a stored backup file.
    /// </summary>
    [HttpGet("download/{fileName}")]
    public async Task<IActionResult> Download(string fileName, CancellationToken cancellationToken)
    {
        var stream = await _backupService.OpenBackupReadAsync(fileName, cancellationToken);
        return File(stream, "application/zip", fileName);
    }
}
