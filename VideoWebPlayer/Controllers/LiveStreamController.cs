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
/// every segment URI carries the token. While no real <c>live.m3u8</c> with at least one
/// referenced segment exists (warm-up, gaps, missing ffmpeg), the in-memory standby stream of
/// <see cref="LiveStreamStandbyProvider"/> answers instead of 404; every delivered response
/// carries <c>Cache-Control: no-store</c> so no stale segment can be replayed.
/// </summary>
[ApiController]
[Route("api/live-stream")]
[BearerTokenCheck]
public class LiveStreamController : ApiBaseController
{
    internal const string M3u8ContentType = "application/vnd.apple.mpegurl";
    internal const string SegmentContentType = "video/mp2t";

    private readonly LiveStreamSegmentStore _segmentStore;
    private readonly LiveStreamStandbyProvider _standby;
    private readonly ProgramSettingsService _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveStreamController"/> class.
    /// </summary>
    /// <param name="segmentStore">The segment directory store.</param>
    /// <param name="standby">The in-memory standby stream for the warm-up phase.</param>
    /// <param name="settings">Program settings holding the TV-channel release switch.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public LiveStreamController(
        LiveStreamSegmentStore segmentStore,
        LiveStreamStandbyProvider standby,
        ProgramSettingsService settings,
        IAuthService authService,
        ILogger<LiveStreamController> logger)
        : base(authService, logger)
    {
        _segmentStore = segmentStore;
        _standby = standby;
        _settings = settings;
    }

    /// <summary>
    /// Gets a file from the segment directory: the <c>.m3u8</c> playlist (rewritten so every
    /// segment URI carries the request's <c>access_token</c>) or a <c>.ts</c> segment. A missing
    /// or segment-less <c>live.m3u8</c> falls back to the standby playlist, a requested
    /// <c>standby_*</c> segment is served from memory; anything else answers 404.
    /// </summary>
    /// <param name="fileName">The requested file name within the segment directory.</param>
    /// <returns>The playlist text, the segment or an error result.</returns>
    [HttpGet("{fileName}")]
    public async Task<IActionResult> GetFile(string fileName)
    {
        // Die 401-Behandlung gilt ausschließlich dem Anmeldefehler — eine
        // UnauthorizedAccessException aus der Datei-Auslieferung ist ein Serverfehler (500).
        try
        {
            CheckLoggedIn();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Ausliefern des Live-Streams");
            return Unauthorized(ex.Message);
        }

        try
        {
            // Feature-Flag: ohne Admin-Freigabe ist der Stream-Endpunkt unsichtbar (404).
            if (!await _settings.GetTvSenderEnabledAsync(HttpContext.RequestAborted))
                return NotFound();

            // N28: ohne Benutzer-Freigabe (Admins implizit) wird der Zugriff verweigert (403).
            if (!await _settings.IsTvSenderAccessGrantedAsync(
                    CurrentUser?.Id, CurrentUser?.IsAdmin ?? false, HttpContext.RequestAborted))
                return Forbid();

            var accessToken = HttpContext.Request.Query["access_token"].ToString();
            var path = await _segmentStore.TryResolveFileAsync(fileName, HttpContext.RequestAborted);

            if (path is not null && fileName.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                var content = await ReadPlaylistAsync(path, HttpContext.RequestAborted);
                if (PlaylistReferencesSegment(content))
                {
                    Response.Headers.CacheControl = "no-store";
                    // Erste echte Playlist nach einer Standby-Auslieferung: der Track-Wechsel
                    // wird einmalig standardkonform markiert (Media-Sequence-Rücksprung).
                    if (_standby.ConsumePendingStandbyTransition())
                        content = InjectDiscontinuity(content);
                    return Content(RewritePlaylistContent(content, accessToken), M3u8ContentType, Encoding.UTF8);
                }

                // Segmentlose Playlist (Torn Read oder unerwartetes ffmpeg-Verhalten) —
                // wird wie eine fehlende Datei behandelt und fällt in den Standby-Pfad.
                path = null;
            }

            if (path is not null)
            {
                Response.Headers.CacheControl = "no-store";
                var contentType = fileName.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                    ? SegmentContentType
                    : "application/octet-stream";
                // ReadWrite|Delete share: ffmpeg and SweepExpiredSegmentsAsync delete expired
                // segments from the rolling window while a client is still downloading them —
                // the FileShare.Read handle PhysicalFile opens internally would make that delete
                // fail under Windows.
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, FileOptions.Asynchronous);
                return File(stream, contentType);
            }

            // Weder echte Playlist noch Disk-Datei: die Warm-up-/Standby-Auslieferung
            // entscheidet (in-memory Playlist bzw. Segment), sonst bleibt es bei 404.
            return await ServeStandbyAsync(fileName, accessToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Ausliefern von '{FileName}'", fileName);
            return StatusCode(500, "Internal server error");
        }
    }

    // Standby-Verzweigung: die Playlist-Anfrage erhält die im Speicher gehaltene
    // Standby-Wiedergabeliste (aktiviert ausstehende Vorbereitung und das
    // Discontinuity-Flag), standby_*-Segmente kommen aus dem aktiven Satz; ohne
    // aktiven Satz (fehlende Assets) bleibt es bei 404.
    private async Task<IActionResult> ServeStandbyAsync(string fileName, string? accessToken)
    {
        if (string.Equals(fileName, FfmpegLiveStreamSegmenter.PlaylistFileName, StringComparison.OrdinalIgnoreCase))
        {
            var configuredId = await _settings.GetLiveStreamStandbyMediaItemIdAsync(HttpContext.RequestAborted);
            var standbyPlaylist = _standby.BuildPlaylist(configuredId);
            if (standbyPlaylist is null)
                return NotFound();

            Response.Headers.CacheControl = "no-store";
            return Content(RewritePlaylistContent(standbyPlaylist, accessToken), M3u8ContentType, Encoding.UTF8);
        }

        var segment = _standby.TryGetSegment(fileName);
        if (segment is not null)
        {
            Response.Headers.CacheControl = "no-store";
            return File(segment, SegmentContentType);
        }

        return NotFound();
    }

    // ReadWrite|Delete share: ffmpeg rewrites the playlist in place while we read it —
    // a FileShare.Read handle would make its write fail and abort the muxer.
    private static async Task<string> ReadPlaylistAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    // The HLS muxer creates live.m3u8 only once the first finished segment exists, but a
    // torn read during its in-place rewrite can still expose a header-only file: only a
    // playlist referencing at least one media URI counts as "the real stream is up".
    private static bool PlaylistReferencesSegment(string content)
        => content.Split('\n').Any(line => !line.StartsWith('#') && !string.IsNullOrWhiteSpace(line));

    // Inserts #EXT-X-DISCONTINUITY before the first media URI — the real playlist restarts
    // its media sequence at 0 while the standby playlist counted wall-clock based, so the
    // player must treat the boundary as a track change.
    private static string InjectDiscontinuity(string content)
    {
        var builder = new StringBuilder();
        var injected = false;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (!injected && !line.StartsWith('#') && !string.IsNullOrWhiteSpace(line))
            {
                builder.AppendLine("#EXT-X-DISCONTINUITY");
                injected = true;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    // Rewrites the playlist so every media URI carries the access_token the request was
    // authenticated with — relative segment URIs would otherwise resolve without the query
    // string and lose authorization. Shared by the real and the standby playlist.
    private static string RewritePlaylistContent(string content, string? accessToken)
    {
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
