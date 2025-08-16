using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

[ApiController]
[Route("api/favorites")]
[Authorize] // <-- Damit ist der User im Controller verfügbar!
public class FavoritesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public FavoritesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = _userManager.GetUserId(User);
        var favorites = await _db.FavoriteEntries
            .Where(f => f.UserId == userId)
            .ToListAsync();
        return Ok(favorites);
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddFavorite([FromBody] FavoriteEntry entry)
    {
        var userId = _userManager.GetUserId(User);
        entry.UserId = userId;
        entry.CreatedAt = DateTime.UtcNow;
        _db.FavoriteEntries.Add(entry);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("remove")]
    public async Task<IActionResult> RemoveFavorite([FromBody] FavoriteEntry entry)
    {
        var userId = _userManager.GetUserId(User);
        var fav = await _db.FavoriteEntries
            .FirstOrDefaultAsync(f => f.UserId == userId &&
                (f.MovieCollectionId == entry.MovieCollectionId ||
                 f.TVShowId == entry.TVShowId ||
                 f.MovieId == entry.MovieId));
        if (fav != null)
        {
            _db.FavoriteEntries.Remove(fav);
            await _db.SaveChangesAsync();
        }
        return Ok();
    }
}