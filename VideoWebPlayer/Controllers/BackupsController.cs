using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using msTools.Backup;

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

    /// <summary>
    /// Creates a new controller.
    /// </summary>
    public BackupsController(IBackupService backupService)
    {
        _backupService = backupService;
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
