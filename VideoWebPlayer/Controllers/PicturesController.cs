using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class PicturesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public PicturesController(ApplicationDbContext db, IAuthService authService, ILogger<PicturesController> logger) : base(authService, logger)
    {
        _db = db;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPicture(long id)
    {
        try
        {
            CheckLogedIn();
            var picture = await _db.Pictures.FindAsync(id);
            if (picture != null && picture.Data.Length > 0)
                return File(picture.Data, picture.ContentType ?? "image/jpg");

            var placeholderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/placeholder.png");
            if (System.IO.File.Exists(placeholderPath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(placeholderPath);
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

