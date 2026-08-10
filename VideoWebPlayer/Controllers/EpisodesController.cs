using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodesController"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="cache">Memory cache instance.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public EpisodesController(ApplicationDbContext db, IMemoryCache cache, IAuthService authService, ILogger<EpisodesController> logger) : base(authService, logger)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// Gets the generated background image of an episode, falling back to its banner, fanart or a placeholder image.
    /// </summary>
    /// <param name="episodeId">The episode identifier.</param>
    /// <returns>The background image content.</returns>
    [HttpGet("{episodeId}/background-image")]
    public async Task<IActionResult> GetBackgroundImage(long episodeId)
    {
        try
        {
            CheckLogedIn();

            var episode = await _db.TVShowEpisodes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == episodeId);
            if (episode is null)
                return NotFound();

            Picture? picture = null;
            if (episode.GeneratedBackgroundPictureId.HasValue)
            {
                picture = await _db.Pictures.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == episode.GeneratedBackgroundPictureId.Value && p.IsGeneratedBackground);
            }

            picture ??= await GetFallbackPictureAsync(episode);

            if (picture is not null && picture.Data is not null && picture.Data.Length > 0)
            {
                Response.Headers["Cache-Control"] = "public, max-age=31536000";
                return File(picture.Data, picture.ContentType ?? "image/jpeg");
            }

            var placeholderBytes = await GetPlaceholderBytesAsync(_cache);
            if (placeholderBytes is not null)
                return File(placeholderBytes, "image/png");

            return NotFound();
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
