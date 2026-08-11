using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Services.EpisodeBackgroundImage;

/// <summary>
/// Provides endpoints for retrieving episode-related images.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class EpisodesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly EpisodeBackgroundImageService _backgroundImageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodesController"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="cache">Memory cache instance.</param>
    /// <param name="backgroundImageService">Service used to lazily ensure an episode's generated background image.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public EpisodesController(ApplicationDbContext db, IMemoryCache cache, EpisodeBackgroundImageService backgroundImageService, IAuthService authService, ILogger<EpisodesController> logger) : base(authService, logger)
    {
        _db = db;
        _cache = cache;
        _backgroundImageService = backgroundImageService;
    }

    /// <summary>
    /// Gets the generated background image of an episode, generating it lazily if necessary and possible,
    /// and falling back to its banner, fanart or a placeholder image otherwise.
    /// </summary>
    /// <param name="episodeId">The episode identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The background image content.</returns>
    [HttpGet("{episodeId}/background-image")]
    public async Task<IActionResult> GetBackgroundImage(long episodeId, CancellationToken cancellationToken)
    {
        try
        {
            CheckLogedIn();

            var episode = await _db.TVShowEpisodes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == episodeId, cancellationToken);
            if (episode is null)
                return NotFound();

            var picture = await _backgroundImageService.EnsureBackgroundImageAsync(episode, cancellationToken);
            picture ??= await GetFallbackPictureAsync(episode);

            if (picture is not null && picture.Data is not null && picture.Data.Length > 0)
            {
                var etag = $"\"{picture.Id}\"";
                Response.Headers["Cache-Control"] = "public, max-age=3600, must-revalidate";
                Response.Headers["ETag"] = etag;

                if (Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) && ifNoneMatch == etag)
                    return StatusCode(304);

                return File(picture.Data, picture.ContentType ?? "image/jpeg");
            }

            var placeholderBytes = await GetPlaceholderBytesAsync(_cache);
            if (placeholderBytes is not null)
                return File(placeholderBytes, "image/png");

            return NotFound();
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Abruf des Hintergrundbilds wurde vom Client abgebrochen");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen des Hintergrundbilds");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen des Hintergrundbilds");
            return StatusCode(500, "Internal server error");
        }
    }

    private async Task<Picture?> GetFallbackPictureAsync(TVShowEpisode episode)
    {
        var fallbackId = episode.BannerPictureId ?? episode.FanartPictureId;
        if (!fallbackId.HasValue)
            return null;

        return await _db.Pictures.AsNoTracking().FirstOrDefaultAsync(p => p.Id == fallbackId.Value);
    }
}
