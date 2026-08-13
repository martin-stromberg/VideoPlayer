using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Services.HomeBackgroundImage;

/// <summary>
/// Provides endpoints for retrieving pictures.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class PicturesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly HomeBackgroundImageGenerator _heroBackgroundGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PicturesController"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="cache">Memory cache instance.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="heroBackgroundGenerator">Generator for hero background images.</param>
    /// <param name="logger">Logger instance.</param>
    public PicturesController(ApplicationDbContext db, IMemoryCache cache, IAuthService authService, HomeBackgroundImageGenerator heroBackgroundGenerator, ILogger<PicturesController> logger) : base(authService, logger)
    {
        _db = db;
        _cache = cache;
        _heroBackgroundGenerator = heroBackgroundGenerator;
    }

    /// <summary>
    /// Gets a picture by identifier.
    /// </summary>
    /// <param name="id">The picture identifier.</param>
    /// <returns>The picture content.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPicture(long id)
    {
        try
        {
            CheckLogedIn();
            var picture = await _db.Pictures.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (picture != null && picture.Data.Length > 0)
                return File(picture.Data, picture.ContentType ?? "image/jpg");

            var placeholderBytes = await GetPlaceholderBytesAsync(_cache);
            if (placeholderBytes is not null)
                return File(placeholderBytes, "image/png");

            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen des Bildes");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen des Bildes");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets a generated hero background image composed from the continue-watching list.
    /// </summary>
    [HttpGet("hero-background")]
    public async Task<IActionResult> GetHeroBackground(CancellationToken cancellationToken)
    {
        try
        {
            CheckLogedIn();
            var image = await _heroBackgroundGenerator.GenerateAsync(User, cancellationToken: cancellationToken);
            if (image is not null)
                return File(image, "image/jpeg");

            var placeholderBytes = await GetPlaceholderBytesAsync(_cache);
            if (placeholderBytes is not null)
                return File(placeholderBytes, "image/png");

            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen des Hero-Hintergrundbilds");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen des Hero-Hintergrundbilds");
            return StatusCode(500, "Internal server error");
        }
    }
}

