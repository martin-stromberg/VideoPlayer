using System.Text;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Delivers the automated TV channel's live stream: the HLS playlist and its segments from the
/// segment directory, only for signed-in users. Since video players cannot send authorization
/// headers, the established <c>access_token</c> query pattern applies — and because relative
/// segment URIs would lose the playlist's query string, the playlist is rewritten on delivery so
/// every segment URI carries the token.
/// </summary>
[ApiController]
[Route("api/live-stream")]
[BearerTokenCheck]
public class LiveStreamController : ApiBaseController
{
    internal const string M3u8ContentType = "application/vnd.apple.mpegurl";
    internal const string SegmentContentType = "video/mp2t";

    private readonly LiveStreamSegmentStore _segmentStore;
    private readonly ProgramSettingsService _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveStreamController"/> class.
    /// </summary>
    /// <param name="segmentStore">The segment directory store.</param>
    /// <param name="settings">Program settings holding the TV-channel release switch.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public LiveStreamController(LiveStreamSegmentStore segmentStore, ProgramSettingsService settings, IAuthService authService, ILogger<LiveStreamController> logger)
        : base(authService, logger)
    {
        _segmentStore = segmentStore;
        _settings = settings;
    }

    /// <summary>
    /// Gets a file from the segment directory: the <c>.m3u8</c> playlist (rewritten so every
    /// segment URI carries the request's <c>access_token</c>) or a <c>.ts</c> segment. Answers
    /// 404 for invalid names, traversal attempts and missing files — including the state where no
    /// stream is currently produced.
    /// </summary>
    /// <param name="fileName">The requested file name within the segment directory.</param>
    /// <returns>The playlist text or the segment file, or an error result.</returns>
    [HttpGet("{fileName}")]
    public async Task<IActionResult> GetFile(string fileName)
    {
        try
        {
            CheckLoggedIn();

            // Feature-Flag: ohne Admin-Freigabe ist der Stream-Endpunkt unsichtbar (404).
            if (!await _settings.GetTvSenderEnabledAsync(HttpContext.RequestAborted))
                return NotFound();

            // N28: ohne Benutzer-Freigabe (Admins implizit) wird der Zugriff verweigert (403).
            if (!await _settings.IsTvSenderAccessGrantedAsync(
                    CurrentUser?.Id, CurrentUser?.IsAdmin ?? false, HttpContext.RequestAborted))
                return Forbid();

            var path = await _segmentStore.TryResolveFileAsync(fileName, HttpContext.RequestAborted);
            if (path is null)
                return NotFound();

            if (fileName.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers.CacheControl = "no-store";
                var rewritten = await RewritePlaylistAsync(path, HttpContext.RequestAborted);
                return Content(rewritten, M3u8ContentType, Encoding.UTF8);
            }

            var contentType = fileName.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                ? SegmentContentType
                : "application/octet-stream";
            return PhysicalFile(path, contentType);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Ausliefern des Live-Streams");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError(ex, "Fehler beim Ausliefern von '{FileName}'", fileName);
            return StatusCode(500, "Internal server error");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Ausliefern des Live-Streams");
            return StatusCode(500, "Internal server error");
        }
    }

    // Rewrites the playlist so every media URI carries the access_token the request was
    // authenticated with — relative segment URIs would otherwise resolve without the query
    // string and lose authorization.
    private async Task<string> RewritePlaylistAsync(string path, CancellationToken cancellationToken)
    {
        var accessToken = HttpContext.Request.Query["access_token"].ToString();
        // ReadWrite|Delete share: ffmpeg rewrites the playlist in place while we read it —
        // a FileShare.Read handle would make its write fail and abort the muxer.
        string content;
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete))
        {
            using var reader = new StreamReader(stream);
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        var builder = new StringBuilder();

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(accessToken) || line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
            {
                builder.AppendLine(line);
                continue;
            }

            var separator = line.Contains('?') ? "&" : "?";
            builder.Append(line).Append(separator).Append("access_token=").Append(Uri.EscapeDataString(accessToken)).AppendLine();
        }

        return builder.ToString();
    }
}
