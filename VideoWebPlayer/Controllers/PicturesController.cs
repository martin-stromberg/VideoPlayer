using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="PicturesController"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="cache">Memory cache instance.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public PicturesController(ApplicationDbContext db, IMemoryCache cache, IAuthService authService, ILogger<PicturesController> logger) : base(authService, logger)
    {
        _db = db;
        _cache = cache;
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

            var placeholderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/placeholder.png");
            if (System.IO.File.Exists(placeholderPath))
            {
                var bytes = await _cache.GetOrCreateAsync("PicturesController.Placeholder", async entry =>
                {
                    entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                    return await System.IO.File.ReadAllBytesAsync(placeholderPath);
                });
                if (bytes is not null)
                    return File(bytes, "image/png");
            }
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
}

