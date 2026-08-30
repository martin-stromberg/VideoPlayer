using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers
{
    /// <summary>
    /// Provides endpoints for listing and viewing actors.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [BearerTokenCheck]
    public class ActorsController : ApiBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IUnlockedMediaService _unlockedMediaService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorsController"/> class.
        /// </summary>
        public ActorsController(IAuthService authService, ApplicationDbContext db, IUnlockedMediaService unlockedMediaService, ILogger<ActorsController> logger)
            : base(authService, logger)
        {
            _db = db;
            _unlockedMediaService = unlockedMediaService;
        }

        /// <summary>
        /// Gets actors, optionally filtered by search term and initial letter.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetActors([FromQuery] string? search, [FromQuery] string? initial)
        {
            try
            {
                CheckLoggedIn();

                var allowedSourceIds = await GetAllowedSourceIdsAsync();
                if (allowedSourceIds.Count == 0)
                    return Ok(Array.Empty<ActorDto>());

                var actorIdsInSources = await _db.MovieActors
                    .Where(ma => allowedSourceIds.Contains(ma.Movie.MediaSourceId))
                    .Select(ma => ma.ActorId)
                    .Union(
                        _db.TVShowEpisodeActors
                            .Where(ea => allowedSourceIds.Contains(ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId))
                            .Select(ea => ea.ActorId))
                    .ToListAsync();

                var query = _db.Actors
                    .AsNoTracking()
                    .Where(a => actorIdsInSources.Contains(a.Id));

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim().ToUpperInvariant();
                    query = query.Where(a => a.NormalizedName.Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(initial))
                {
                    var letter = initial.ToUpperInvariant()[0];
                    query = query.Where(a => a.NormalizedName.StartsWith(letter.ToString()));
                }

                var actors = await query
                    .OrderBy(a => a.NormalizedName)
                    .Select(a => new ActorDto { Id = a.Id, Name = a.Name })
                    .ToListAsync();

                return Ok(actors);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Schauspieler");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Abrufen der Schauspieler");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets details for a specific actor.
        /// </summary>
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetActor(long id)
        {
            try
            {
                CheckLoggedIn();

                var actor = await _db.Actors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id);
                if (actor is null)
                    return NotFound("Schauspieler nicht gefunden.");

                var allowedSourceIds = await GetAllowedSourceIdsAsync();

                var movies = await _db.MovieActors
                    .AsNoTracking()
                    .Where(ma => ma.ActorId == id && allowedSourceIds.Contains(ma.Movie.MediaSourceId))
                    .Select(ma => new ActorMediaEntryDto
                    {
                        Type = "Movie",
                        Id = ma.Movie.Id,
                        Title = ma.Movie.Name,
                        Subtitle = ma.Movie.MovieCollection != null ? ma.Movie.MovieCollection.Name : null
                    })
                    .ToListAsync();

                var episodes = await _db.TVShowEpisodeActors
                    .AsNoTracking()
                    .Where(ea => ea.ActorId == id && allowedSourceIds.Contains(ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId))
                    .Select(ea => new ActorMediaEntryDto
                    {
                        Type = "Episode",
                        Id = ea.TVShowEpisode.Id,
                        Title = ea.TVShowEpisode.Name,
                        Subtitle = $"{ea.TVShowEpisode.TVShowSeason.TVShow.Name} - {ea.TVShowEpisode.TVShowSeason.Name}"
                    })
                    .ToListAsync();

                var dto = new ActorDetailsDto
                {
                    Id = actor.Id,
                    Name = actor.Name,
                    Media = [.. movies, .. episodes]
                };

                return Ok(dto);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Schauspielerdetails");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Abrufen der Schauspielerdetails");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the first letters for which actors exist.
        /// </summary>
        [HttpGet("initials")]
        public async Task<IActionResult> GetInitials()
        {
            try
            {
                CheckLoggedIn();

                var allowedSourceIds = await GetAllowedSourceIdsAsync();
                if (allowedSourceIds.Count == 0)
                    return Ok(Array.Empty<char>());

                var actorIds = await _db.MovieActors
                    .Where(ma => allowedSourceIds.Contains(ma.Movie.MediaSourceId))
                    .Select(ma => ma.ActorId)
                    .Union(
                        _db.TVShowEpisodeActors
                            .Where(ea => allowedSourceIds.Contains(ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId))
                            .Select(ea => ea.ActorId))
                    .ToListAsync();

                var initials = await _db.Actors
                    .AsNoTracking()
                    .Where(a => actorIds.Contains(a.Id))
                    .Select(a => a.NormalizedName.Substring(0, 1))
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                return Ok(initials);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Schauspieler-Anfangsbuchstaben");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Abrufen der Schauspieler-Anfangsbuchstaben");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<List<long>> GetAllowedSourceIdsAsync()
        {
            var sourceIds = await _db.MediaSourceUsers
                .AsNoTracking()
                .Where(msu => msu.UserId == CurrentUser!.Id)
                .Select(msu => msu.MediaSourceId)
                .ToListAsync();

            var unlockedSourceIds = await _unlockedMediaService.GetUnlockedSourceIdsForUserAsync(CurrentUser!.Id);
            return sourceIds.Union(unlockedSourceIds).ToList();
        }
    }
}
