using Microsoft.AspNetCore.Antiforgery;
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
    private readonly ManualBackupJobService _manualBackupJobs;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<BackupsController> _logger;

    /// <summary>
    /// Creates a new controller.
    /// </summary>
    public BackupsController(
        IBackupService backupService,
        ManualBackupJobService manualBackupJobs,
        IAntiforgery antiforgery,
        ILogger<BackupsController> logger)
    {
        _backupService = backupService;
        _manualBackupJobs = manualBackupJobs;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    /// <summary>
    /// Creates a manual backup from a regular server-side form post.
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await _antiforgery.ValidateRequestAsync(HttpContext);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation("Manual backup job requested by user {UserId}.", userId);

        var result = _manualBackupJobs.StartManualBackup(userId);
        var message = result.Started
            ? "Backup wurde im Hintergrund gestartet."
            : "Es läuft bereits ein manuelles Backup.";

        return Redirect($"/admin/backups?backupStatus={Uri.EscapeDataString(message)}");
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
