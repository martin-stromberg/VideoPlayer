using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
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
    private readonly IWebHostEnvironment _env;
    private readonly IUnlockedMediaService _unlockedMediaService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenresController"/> class.
    /// </summary>
    /// <param name="authService">Authentication service.</param>
    /// <param name="db">Database context.</param>
    /// <param name="env">Web host environment.</param>
    /// <param name="unlockedMediaService">Unlocked media service.</param>
    /// <param name="logger">Logger instance.</param>
    public SourceGenresController(IAuthService authService, ApplicationDbContext db, IWebHostEnvironment env, IUnlockedMediaService unlockedMediaService, ILogger<SourceGenresController> logger)
        :base(authService, logger)
    {
        _db = db;
        _env = env;
        _unlockedMediaService = unlockedMediaService;
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
                    Genres = genres.Select(g => new GenreDto { Id = g.Id, Name = g.Name, IconUrl = GetGenreImageUrl(g.Name) }).ToList()
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

            // Pr?fe, ob die Quelle f?r den Benutzer freigeschaltet ist
            var isAllowed = await _db.MediaSourceUsers
                .AsNoTracking()
                .AnyAsync(msu => msu.UserId == CurrentUser.Id && msu.MediaSourceId == sourceId);
            var unlockedSourceIds = await _unlockedMediaService.GetUnlockedSourceIdsForUserAsync(CurrentUser.Id);

            if (!isAllowed && !unlockedSourceIds.Contains(sourceId))
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
                Genres = genres.Select(g => new GenreDto { Id = g.Id, Name = g.Name, IconUrl = GetGenreImageUrl(g.Name) }).OrderBy(g => g.Name).ToList()
            };

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Genres f?r Quelle");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Genres f?r Quelle");
            return StatusCode(500, "Internal server error");
        }
    }

    private string? GetGenreImageUrl(string genreName)
    {
        if (string.IsNullOrWhiteSpace(genreName))
            return null;

        var key = GetGenreImageKey(genreName);
        var iconsDir = Path.Combine(_env.WebRootPath, "images", "genres");
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" })
        {
            var absPath = Path.Combine(iconsDir, $"{key}{ext}");
            if (System.IO.File.Exists(absPath))
            {
                return $"/images/genres/{key}{ext}";
            }
        }

        return null;
    }

    private static string GetGenreImageKey(string genreName)
    {
        var normalized = genreName.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
