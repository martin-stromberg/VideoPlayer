using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

/// <summary>
/// Provides endpoints for managing user favorites.
/// </summary>
[ApiController]
[Route("api/favorites")]
[BearerTokenCheck]
public class FavoritesController : ApiBaseController
{
    private readonly IFavoritesService _favoritesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FavoritesController"/> class.
    /// </summary>
    /// <param name="favoritesService">Favorites service.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public FavoritesController(IFavoritesService favoritesService, IAuthService authService, ILogger<FavoritesController> logger)
        : base(authService, logger)
    {
        _favoritesService = favoritesService;
    }

    /// <summary>
    /// Gets the favorites for the current user.
    /// </summary>
    /// <returns>The favorites list.</returns>
    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        try
        {
            CheckLogedIn();
            var result = await _favoritesService.GetFavoritesAsync(CurrentUser!.Id, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Favoriten");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Favoriten");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Adds a favorite entry for the current user.
    /// </summary>
    /// <param name="entry">The favorite entry.</param>
    /// <returns>The action result.</returns>
    [HttpPost("add")]
    public async Task<IActionResult> AddFavorite([FromBody] FavoriteEntry entry)
    {
        try
        {
            CheckLogedIn();
            await _favoritesService.AddFavoriteAsync(CurrentUser!.Id, entry, HttpContext.RequestAborted);
            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Favoriten");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Favoriten");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Removes a favorite entry for the current user.
    /// </summary>
    /// <param name="entry">The favorite entry.</param>
    /// <returns>The action result.</returns>
    [HttpPost("remove")]
    public async Task<IActionResult> RemoveFavorite([FromBody] FavoriteEntry entry)
    {
        try
        {
            CheckLogedIn();
            await _favoritesService.RemoveFavoriteAsync(CurrentUser!.Id, entry, HttpContext.RequestAborted);
            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Entfernen der Favoriten");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Entfernen der Favoriten");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Toggles a favorite entry for the current user.
    /// </summary>
    /// <param name="entry">The media entry to toggle.</param>
    /// <returns><c>true</c> if the entry is now favorited.</returns>
    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleFavorite([FromBody] DtoMediaEntry entry)
    {
        CheckLogedIn();
        var isFav = await _favoritesService.ToggleFavoriteAsync(CurrentUser!.Id, entry, HttpContext.RequestAborted);
        return Ok(isFav);
    }
}