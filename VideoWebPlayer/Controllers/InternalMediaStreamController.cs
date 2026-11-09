using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Internal, token-secured media stream endpoint used by the duration probe's HTTP path and
/// by the live-stream segmenter: the server hands ffprobe a loopback URI containing a
/// short-lived token (<see cref="IInternalMediaStreamTokenService.TryCreateProbeUri"/>) and
/// hands the segmenter's ffmpeg input a URI whose token lives for the remaining slot runtime
/// plus margin (<see cref="IInternalMediaStreamTokenService.TryCreateStreamUri"/>). Each token
/// authorizes streaming of exactly one media item — no user login involved. The endpoint
/// intentionally does not carry <see cref="BearerTokenCheckAttribute"/> or user-based access
/// checks; the unguessable, expiring token issued by
/// <see cref="IInternalMediaStreamTokenService"/> is the only credential. Like every other
/// TV-channel endpoint it follows the global release switch <see cref="Setup.TvSenderEnabled"/>
/// and answers 404 while the feature is unreleased. It serves the file through the same
/// <see cref="IMediaSourceReader"/> path as playback and with HTTP range processing, so a
/// probing client can seek remotely instead of downloading the whole file.
/// </summary>
[ApiController]
[Route("api/internal/media-stream")]
public class InternalMediaStreamController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMediaSourceReader _reader;
    private readonly IInternalMediaStreamTokenService _tokens;
    private readonly ProgramSettingsService _settings;
    private readonly ILogger<InternalMediaStreamController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalMediaStreamController"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="reader">Media source reader used to open the file.</param>
    /// <param name="tokens">Internal stream token service.</param>
    /// <param name="settings">Program settings holding the TV-channel release switch.</param>
    /// <param name="logger">Logger instance.</param>
    public InternalMediaStreamController(
        ApplicationDbContext db,
        IMediaSourceReader reader,
        IInternalMediaStreamTokenService tokens,
        ProgramSettingsService settings,
        ILogger<InternalMediaStreamController> logger)
    {
        _db = db;
        _reader = reader;
        _tokens = tokens;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Streams the media item the given stream token was issued for, with range processing
    /// so clients (e.g. ffprobe) can seek. Unknown or expired tokens answer 404 without
    /// revealing whether the token ever existed.
    /// </summary>
    /// <param name="token">The stream token issued for one media item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The media file as a range-processed stream.</returns>
    [HttpGet("{token}")]
    public async Task<IActionResult> Stream(string token, CancellationToken cancellationToken)
    {
        // Feature-Flag: ohne Admin-Freigabe ist der Endpunkt unsichtbar (404) — geprüft vor
        // dem Token-Resolve, damit das Feature wie an den übrigen TV-Sender-Gates von
        // „existiert nicht" ununterscheidbar bleibt.
        if (!await _settings.GetTvSenderEnabledAsync(cancellationToken))
            return NotFound();

        var mediaItemId = _tokens.ResolveMediaItemId(token);
        if (mediaItemId is null)
            return NotFound();

        var mediaItem = await _db.MediaItems
            .Include(mi => mi.MediaCollection)
            .ThenInclude(mc => mc.MediaSource)
            .FirstOrDefaultAsync(mi => mi.Id == mediaItemId.Value, cancellationToken);
        if (mediaItem is null)
            return NotFound();

        var fileName = Path.GetFileName(mediaItem.Path);

        Stream? stream;
        try
        {
            stream = _reader.OpenFileStream(mediaItem.MediaCollection, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Interner Media-Stream für MediaItem {MediaItemId} konnte nicht geöffnet werden.", mediaItem.Id);
            return StatusCode(500, "Internal server error");
        }

        if (stream is null)
            return NotFound();

        return File(stream, MediaFileContentTypes.ForFileName(fileName), enableRangeProcessing: true);
    }
}
