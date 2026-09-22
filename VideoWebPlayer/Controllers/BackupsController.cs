using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
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
    private readonly VideoWebPlayerBackupFacade _backupFacade;
    private readonly BackupUploadSessionService _uploadSessions;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<BackupsController> _logger;

    /// <summary>
    /// Creates a new controller.
    /// </summary>
    public BackupsController(
        IBackupService backupService,
        ManualBackupJobService manualBackupJobs,
        VideoWebPlayerBackupFacade backupFacade,
        BackupUploadSessionService uploadSessions,
        IAntiforgery antiforgery,
        ILogger<BackupsController> logger)
    {
        _backupService = backupService;
        _manualBackupJobs = manualBackupJobs;
        _backupFacade = backupFacade;
        _uploadSessions = uploadSessions;
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
    /// Receives a single chunk of a resumable backup upload as application/octet-stream.
    /// </summary>
    [HttpPost("upload/chunk")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadChunk(CancellationToken cancellationToken)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new { error = "Ungültiges Sicherheitstoken." });
        }

        if (!IsOctetStream(Request.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { error = "Nur application/octet-stream wird unterstützt." });

        if (!TryGetInt64Header("Upload-Offset", out var uploadOffset) || uploadOffset < 0)
            return BadRequest(new { error = "Upload-Offset fehlt oder ist ungültig." });

        var contentLength = Request.ContentLength;
        if (contentLength is null or < 0)
            return BadRequest(new { error = "Content-Length fehlt oder ist ungültig." });

        var (session, sessionError) = await ResolveOrBeginSessionAsync(cancellationToken);
        if (session is null)
            return sessionError!;

        var appendResult = await session.AppendChunkAsync(uploadOffset, Request.Body, contentLength.Value, cancellationToken);
        if (appendResult.Status == BackupUploadAppendStatus.Rejected)
            return BadRequest(new { error = appendResult.Error });

        if (appendResult.Status == BackupUploadAppendStatus.ResumeRequired)
        {
            Response.Headers["Upload-Offset"] = session.ReceivedBytes.ToString();
            return StatusCode(StatusCodes.Status308PermanentRedirect);
        }

        if (session.ReceivedBytes < session.TotalLength)
        {
            Response.Headers["Upload-Id"] = session.Id.ToString();
            Response.Headers["Upload-Offset"] = session.ReceivedBytes.ToString();
            return NoContent();
        }

        await _uploadSessions.CompleteSessionAsync(session);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _backupFacade.ImportUploadFileAsync(session.TempPath, session.FileName, userId, cancellationToken);
        if (!result.Succeeded)
        {
            await _uploadSessions.AbortSessionAsync(session);
            var error = string.Join(" ", new[] { result.Message }.Concat(result.Errors).Distinct());
            return BadRequest(new { error });
        }

        return Ok(new { message = result.Message, fileName = session.FileName });
    }

    /// <summary>
    /// Returns the current state of a resumable backup upload.
    /// </summary>
    [HttpGet("upload/{uploadId:guid}")]
    public IActionResult GetUploadStatus(Guid uploadId)
    {
        var session = _uploadSessions.GetSession(uploadId);
        if (session is null)
            return NotFound();

        Response.Headers["Upload-Offset"] = session.ReceivedBytes.ToString();
        return Ok(new BackupUploadStatusResponse(session.Id, session.FileName, session.TotalLength, session.ReceivedBytes));
    }

    /// <summary>
    /// Aborts a resumable backup upload and deletes its temp file.
    /// </summary>
    [HttpDelete("upload/{uploadId:guid}")]
    public async Task<IActionResult> AbortUpload(Guid uploadId)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new { error = "Ungültiges Sicherheitstoken." });
        }

        var session = _uploadSessions.GetSession(uploadId);
        if (session is null)
            return NotFound();

        await _uploadSessions.AbortSessionAsync(session);
        return NoContent();
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

    private async Task<(BackupUploadSession? Session, IActionResult? Error)> ResolveOrBeginSessionAsync(CancellationToken cancellationToken)
    {
        var uploadIdHeader = Request.Headers["Upload-Id"].ToString();
        if (string.IsNullOrWhiteSpace(uploadIdHeader))
            return await BeginNewSessionAsync(cancellationToken);

        if (!Guid.TryParse(uploadIdHeader, out var uploadId))
            return (null, BadRequest(new { error = "Upload-Id ist ungültig." }));

        var session = _uploadSessions.GetSession(uploadId);
        if (session is null)
            return (null, NotFound(new { error = "Upload-Session unbekannt oder abgelaufen." }));

        var sentName = Request.Headers["Upload-Name"].ToString();
        if (!string.IsNullOrEmpty(sentName)
            && !string.Equals(Uri.UnescapeDataString(sentName), session.FileName, StringComparison.Ordinal))
        {
            return (null, StatusCode(StatusCodes.Status409Conflict, new { error = "Upload-Name stimmt nicht mit der Session überein." }));
        }

        var sentLength = Request.Headers["Upload-Length"].ToString();
        if (!string.IsNullOrEmpty(sentLength)
            && (!long.TryParse(sentLength, out var parsedLength) || parsedLength != session.TotalLength))
        {
            return (null, StatusCode(StatusCodes.Status409Conflict, new { error = "Upload-Length stimmt nicht mit der Session überein." }));
        }

        return (session, null);
    }

    private async Task<(BackupUploadSession? Session, IActionResult? Error)> BeginNewSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetInt64Header("Upload-Length", out var uploadLength))
            return (null, BadRequest(new { error = "Upload-Length fehlt oder ist ungültig." }));

        var uploadName = Request.Headers["Upload-Name"].ToString();
        var fileName = string.IsNullOrEmpty(uploadName) ? uploadName : Uri.UnescapeDataString(uploadName);
        var beginResult = await _uploadSessions.BeginSessionAsync(fileName, uploadLength, cancellationToken);
        if (!beginResult.Succeeded)
        {
            return (null, beginResult.ExceedsUploadLimit
                ? StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = beginResult.Error })
                : BadRequest(new { error = beginResult.Error }));
        }

        var session = beginResult.Session!;
        _logger.LogInformation(
            "Backup upload {UploadId} started by user {UserId}: {FileName} ({TotalLength} bytes).",
            session.Id,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            session.FileName,
            session.TotalLength);
        return (session, null);
    }

    private static bool IsOctetStream(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var mediaType = contentType.Split(';')[0].Trim();
        return string.Equals(mediaType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetInt64Header(string name, out long value)
    {
        return long.TryParse(Request.Headers[name].ToString(), out value);
    }
}
