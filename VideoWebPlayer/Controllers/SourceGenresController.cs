using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

/// <summary>
/// Provides endpoints for retrieving genres per media source.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class SourceGenresController : ApiBaseController
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenresController"/> class.
    /// </summary>
    /// <param name="authService">Authentication service.</param>
    /// <param name="db">Database context.</param>
    /// <param name="logger">Logger instance.</param>
    public SourceGenresController(IAuthService authService, ApplicationDbContext db, ILogger<SourceGenresController> logger)
        :base(authService, logger)
    {
        _db = db;
    }

    /// <summary>
    /// Gets genres grouped by source for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetGenresPerSource()
    {
        try
        {
            CheckLogedIn();

            var sourceIds = await _db.MediaSourceUsers
                .AsNoTracking()
                .Where(msu => msu.UserId == CurrentUser.Id)
                .Select(msu => msu.MediaSourceId)
                .ToListAsync();

            var sources = await _db.MediaSources
                .AsNoTracking()
                .Where(ms => sourceIds.Contains(ms.Id))
                .ToListAsync();

            var result = new List<SourceGenresDto>();
            foreach (var source in sources)
            {
                // Genres aus Movies dieser Quelle sammeln
                var movieGenreNames = await _db.MovieGenres
                    .AsNoTracking()
                    .Where(mg => mg.Movie.MediaSourceId == source.Id)
                    .Select(mg => mg.GenreId)
                    .Distinct()
                    .ToListAsync();

                // Genres aus TVShows dieser Quelle sammeln
                var tvShowGenreNames = await _db.TVShowGenres
                    .AsNoTracking()
                    .Where(tvg => tvg.TVShow.MediaSourceId == source.Id)
                    .Select(tvg => tvg.GenreId)
                    .Distinct()
                    .ToListAsync();

                var allGenreNames = movieGenreNames.Concat(tvShowGenreNames).Distinct().ToList();

                var genres = await _db.Genres
                    .AsNoTracking()
                    .Where(g => allGenreNames.Contains(g.Id))
                    .ToListAsync();

                result.Add(new SourceGenresDto
                {
                    SourceId = source.Id,
                    SourceName = source.Name,
                    Genres = genres.Select(g => new GenreDto { Id = g.Id, Name = g.Name }).ToList()
                });
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Quellen-Genres");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Quellen-Genres");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets genres for a specific source.
    /// </summary>
    /// <param name="sourceId">The source identifier.</param>
    [HttpGet("{sourceId:long}")]
    public async Task<IActionResult> GetGenresForSource(long sourceId)
    {
        try
        {
            CheckLogedIn();

            // Prüfe, ob die Quelle für den Benutzer freigeschaltet ist
            var isAllowed = await _db.MediaSourceUsers
                .AsNoTracking()
                .AnyAsync(msu => msu.UserId == CurrentUser.Id && msu.MediaSourceId == sourceId);

            if (!isAllowed)
                return Forbid("Kein Zugriff auf diese Quelle.");

            var source = await _db.MediaSources.AsNoTracking().FirstOrDefaultAsync(ms => ms.Id == sourceId);
            if (source == null)
                return NotFound("Quelle nicht gefunden.");

            // Genres aus Movies dieser Quelle sammeln
            var movieGenreIds = await _db.MovieGenres
                .AsNoTracking()
                .Where(mg => mg.Movie.MediaSourceId == sourceId)
                .Select(mg => mg.GenreId)
                .Distinct()
                .ToListAsync();

            // Genres aus TVShows dieser Quelle sammeln
            var tvShowGenreIds = await _db.TVShowGenres
                .AsNoTracking()
                .Where(tvg => tvg.TVShow.MediaSourceId == sourceId)
                .Select(tvg => tvg.GenreId)
                .Distinct()
                .ToListAsync();

            var allGenreIds = movieGenreIds.Concat(tvShowGenreIds).Distinct().ToList();

            var genres = await _db.Genres
                .AsNoTracking()
                .Where(g => allGenreIds.Contains(g.Id))
                .Where(g => !g.IsHidden)
                .ToListAsync();

            var result = new SourceGenresDto
            {
                SourceId = source.Id,
                SourceName = source.Name,
                Genres = genres.Select(g => new GenreDto { Id = g.Id, Name = g.Name }).OrderBy(g => g.Name).ToList()
            };

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Genres für Quelle");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Genres für Quelle");
            return StatusCode(500, "Internal server error");
        }
    }
}

