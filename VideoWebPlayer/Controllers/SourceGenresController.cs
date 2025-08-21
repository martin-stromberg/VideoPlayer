using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class SourceGenresController : ApiBaseController
{
    private readonly IAuthService _authService;
    private readonly ApplicationDbContext _db;

    public SourceGenresController(IAuthService authService, ApplicationDbContext db, ILogger<SourceGenresController> logger)
        :base(authService, logger)
    {
        _authService = authService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetGenresPerSource()
    {
        try
        {
            CheckLogedIn();

            var sourceIds = await _db.MediaSourceUsers
                .Where(msu => msu.UserId == CurrentUser.Id)
                .Select(msu => msu.MediaSourceId)
                .ToListAsync();

            var sources = await _db.MediaSources
                .Where(ms => sourceIds.Contains(ms.Id))
                .ToListAsync();

            var result = new List<SourceGenresDto>();
            foreach (var source in sources)
            {
                // Genres aus Movies dieser Quelle sammeln
                var movieGenreNames = await _db.MovieGenres
                    .Where(mg => mg.Movie.MediaSourceId == source.Id)
                    .Select(mg => mg.GenreId)
                    .Distinct()
                    .ToListAsync();

                // Genres aus TVShows dieser Quelle sammeln
                var tvShowGenreNames = await _db.TVShowGenres
                    .Where(tvg => tvg.TVShow.MediaSourceId == source.Id)
                    .Select(tvg => tvg.GenreId)
                    .Distinct()
                    .ToListAsync();

                var allGenreNames = movieGenreNames.Concat(tvShowGenreNames).Distinct().ToList();

                var genres = await _db.Genres
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

    [HttpGet("{sourceId:long}")]
    public async Task<IActionResult> GetGenresForSource(long sourceId)
    {
        try
        {
            CheckLogedIn();

            // Prüfe, ob die Quelle für den Benutzer freigeschaltet ist
            var isAllowed = await _db.MediaSourceUsers
                .AnyAsync(msu => msu.UserId == CurrentUser.Id && msu.MediaSourceId == sourceId);

            if (!isAllowed)
                return Forbid("Kein Zugriff auf diese Quelle.");

            var source = await _db.MediaSources.FirstOrDefaultAsync(ms => ms.Id == sourceId);
            if (source == null)
                return NotFound("Quelle nicht gefunden.");

            // Genres aus Movies dieser Quelle sammeln
            var movieGenreIds = await _db.MovieGenres
                .Where(mg => mg.Movie.MediaSourceId == sourceId)
                .Select(mg => mg.GenreId)
                .Distinct()
                .ToListAsync();

            // Genres aus TVShows dieser Quelle sammeln
            var tvShowGenreIds = await _db.TVShowGenres
                .Where(tvg => tvg.TVShow.MediaSourceId == sourceId)
                .Select(tvg => tvg.GenreId)
                .Distinct()
                .ToListAsync();

            var allGenreIds = movieGenreIds.Concat(tvShowGenreIds).Distinct().ToList();

            var genres = await _db.Genres
                .Where(g => allGenreIds.Contains(g.Id))
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

public class SourceGenresDto
{
    public long SourceId { get; set; }
    public string SourceName { get; set; }
    public List<GenreDto> Genres { get; set; } = new();
}

public class GenreDto
{
    public long Id { get; set; }
    public string Name { get; set; }
}