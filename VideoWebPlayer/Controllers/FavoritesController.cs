using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

/// <summary>
/// Provides endpoints for managing user favorites.
/// </summary>
[ApiController]
[Route("api/favorites")]
[BearerTokenCheck]
public class FavoritesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="FavoritesController"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public FavoritesController(ApplicationDbContext db, IAuthService authService, ILogger<FavoritesController> logger)
        :base(authService, logger)
    {
        _db = db;
    }

    private async Task<FavoriteEntry> GetFavoriteEntryAsync(DtoMediaEntry entry)
    {
        var query = _db.FavoriteEntries.AsNoTracking().Where(f => f.UserId == CurrentUser.Id);
        if (entry is DtoMovie)
            return await query.FirstOrDefaultAsync(f => f.MovieId == entry.Id);
        if (entry is DtoMovieCollection)
            return await query.FirstOrDefaultAsync(f => f.MovieCollectionId == entry.Id);
        if (entry is DtoTVShow)
            return await query.FirstOrDefaultAsync(f => f.TVShowId == entry.Id);
        if (entry is DtoTVShowSeason)
            return await query.FirstOrDefaultAsync(f => f.TVShowSeasonId == entry.Id);
        if (entry is DtoTVShowEpisode)
            return await query.FirstOrDefaultAsync(f => f.TVShowEpisodeId == entry.Id);
        return null;
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
            var favorites = await _db.FavoriteEntries
                .AsNoTracking()
                .Where(f => f.UserId == CurrentUser.Id)
                .ToListAsync();
            var result = favorites.Select(rec =>
            {
                var entry = Create<DtoFavoriteEntry>(rec);
                if (rec.MovieId is not null)
                    entry.Entry = _db.Movies.Where(m => m.Id == rec.MovieId).ToList().Select(m =>
                    {
                        var movie = Create<DtoMovie>(m);
                        movie.Collection = _db.MovieCollections
                            .Where(mc => mc.Id == m.MovieCollectionId)
                            .ToList()
                            .Select(mc => Create<DtoMovieCollection>(mc))
                            .FirstOrDefault();
                        return movie;
                    }).FirstOrDefault();
                else if (rec.MovieCollectionId is not null)
                    entry.Entry = _db.MovieCollections.Where(m => m.Id == rec.MovieCollectionId).ToList().Select(m => Create<DtoMovieCollection>(m)).FirstOrDefault();
                else if (rec.TVShowId is not null)
                    entry.Entry = _db.TVShows.Where(m => m.Id == rec.TVShowId).ToList().Select(m => Create<DtoTVShow>(m)).FirstOrDefault();
                else if (rec.TVShowSeasonId is not null)
                    entry.Entry = _db.TVShowSeasons.Where(m => m.Id == rec.TVShowSeasonId).ToList().Select(s =>
                    {
                        var season = Create<DtoTVShowSeason>(s);
                        season.Show = _db.TVShows.Where(m => m.Id == s.TVShowId).ToList().Select(m => Create<DtoTVShow>(m)).FirstOrDefault();
                        return season;
                    }).FirstOrDefault();
                else if (rec.TVShowEpisodeId is not null)
                    entry.Entry = _db.TVShowEpisodes.Where(m => m.Id == rec.TVShowEpisodeId).ToList().Select(e =>
                    {
                        var episode = Create<DtoTVShowEpisode>(e);
                        episode.Season = _db.TVShowSeasons.Where(s => s.Id == e.TVShowSeasonId).ToList().Select(s =>
                        {
                            var season = Create<DtoTVShowSeason>(s);
                            season.Show = _db.TVShows.Where(m => m.Id == s.TVShowId).ToList().Select(m => Create<DtoTVShow>(m)).FirstOrDefault();
                            return season;
                        }).FirstOrDefault();
                        return episode;
                    }).FirstOrDefault();
                if (entry.Entry is null)
                    return null;
                return entry;
            }).Where(e => e is not null).ToArray();
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
            entry.UserId = CurrentUser.Id;
            entry.CreatedAt = DateTime.UtcNow;
            _db.FavoriteEntries.Add(entry);
            await _db.SaveChangesAsync();
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
            var fav = await _db.FavoriteEntries
                .FirstOrDefaultAsync(f => f.UserId == CurrentUser.Id &&
                    ((entry.MovieCollectionId != null && f.MovieCollectionId == entry.MovieCollectionId) ||
                     (entry.TVShowId != null && f.TVShowId == entry.TVShowId) ||
                     (entry.MovieId != null && f.MovieId == entry.MovieId)));
            if (fav != null)
            {
                _db.FavoriteEntries.Remove(fav);
                await _db.SaveChangesAsync();
            }
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
        var exists = await GetFavoriteEntryAsync(entry);
        if (exists is null)
            _ = await AddFavorite(new FavoriteEntry
            {
                UserId = CurrentUser.Id,
                MovieCollectionId = entry is DtoMovieCollection ? entry.Id : null,
                TVShowId = entry is DtoTVShow ? entry.Id : null,
                TVShowSeasonId = entry is DtoTVShowSeason ? entry.Id : null,
                TVShowEpisodeId = entry is DtoTVShowEpisode ? entry.Id : null,
                MovieId = entry is DtoMovie ? entry.Id : null
            });
        else
            _ = await RemoveFavorite(exists);

        exists = await GetFavoriteEntryAsync(entry);
        return Ok(exists is not null);
    }
}