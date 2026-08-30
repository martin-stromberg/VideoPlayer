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
                    .Select(a => new ActorDto
                    {
                        Id = a.Id,
                        Name = a.Name,
                        PictureUrl = a.PictureId.HasValue ? $"/api/pictures/{a.PictureId}" : null
                    })
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
                var setup = await _db.Setups.AsNoTracking().FirstOrDefaultAsync();
                var threshold = (setup?.ActorCollectionThresholdPercent ?? 50) / 100.0;

                var media = await BuildAggregatedMediaAsync(id, allowedSourceIds, threshold, cancellationToken: default);

                var dto = new ActorDetailsDto
                {
                    Id = actor.Id,
                    Name = actor.Name,
                    PictureUrl = actor.PictureId.HasValue ? $"/api/pictures/{actor.PictureId}" : null,
                    Media = media
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

        private async Task<List<ActorMediaEntryDto>> BuildAggregatedMediaAsync(long actorId, List<long> allowedSourceIds, double threshold, CancellationToken cancellationToken)
        {
            var result = new List<ActorMediaEntryDto>();

            var movieActors = await _db.MovieActors
                .AsNoTracking()
                .Where(ma => ma.ActorId == actorId && allowedSourceIds.Contains(ma.Movie.MediaSourceId))
                .Select(ma => new
                {
                    ma.Movie.Id,
                    ma.Movie.Name,
                    ma.Movie.PosterPictureId,
                    CollectionId = (long?)ma.Movie.MovieCollectionId,
                    CollectionName = ma.Movie.MovieCollection != null ? ma.Movie.MovieCollection.Name : null,
                    CollectionPosterPictureId = ma.Movie.MovieCollection != null ? ma.Movie.MovieCollection.PosterPictureId : null
                })
                .ToListAsync(cancellationToken);

            if (movieActors.Count > 0)
            {
                var collectionIds = movieActors
                    .Where(m => m.CollectionId.HasValue)
                    .Select(m => m.CollectionId!.Value)
                    .Distinct()
                    .ToList();

                var collectionTotals = await _db.Movies
                    .AsNoTracking()
                    .Where(m => m.MovieCollectionId.HasValue && collectionIds.Contains(m.MovieCollectionId.Value) && allowedSourceIds.Contains(m.MediaSourceId))
                    .GroupBy(m => m.MovieCollectionId!.Value)
                    .Select(g => new { CollectionId = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(g => g.CollectionId, g => g.Total, cancellationToken);

                foreach (var group in movieActors.GroupBy(m => m.CollectionId))
                {
                    var items = group.ToList();
                    if (!group.Key.HasValue)
                    {
                        foreach (var movie in items)
                        {
                            result.Add(new ActorMediaEntryDto { Type = "Film", Id = movie.Id, Title = movie.Name, PictureUrl = GetPictureUrl(movie.PosterPictureId), LinkUrl = movie.CollectionId.HasValue ? $"/moviecollection/{movie.CollectionId}?movie={movie.Id}" : "#" });
                        }
                        continue;
                    }

                    var collectionId = group.Key.Value;
                    var collectionName = items.First().CollectionName ?? "Filmsammlung";
                    var totalMovies = collectionTotals.GetValueOrDefault(collectionId);
                    var actorCount = items.Count;

                    if (totalMovies == 1 || actorCount == 1)
                    {
                        foreach (var movie in items)
                            result.Add(new ActorMediaEntryDto { Type = "Film", Id = movie.Id, Title = movie.Name, PictureUrl = GetPictureUrl(movie.PosterPictureId), LinkUrl = $"/moviecollection/{collectionId}?movie={movie.Id}" });
                        continue;
                    }

                    if (actorCount == totalMovies)
                    {
                        result.Add(new ActorMediaEntryDto { Type = "Filmsammlung", Id = collectionId, Title = collectionName, Subtitle = $"{actorCount} Filme", PictureUrl = GetPictureUrl(items.First().CollectionPosterPictureId), LinkUrl = $"/moviecollection/{collectionId}" });
                        continue;
                    }

                    if (totalMovies > 0 && (double)actorCount / totalMovies >= threshold)
                    {
                        var titles = string.Join(", ", items.OrderBy(m => m.Name).Select(m => m.Name));
                        result.Add(new ActorMediaEntryDto { Type = "Filmsammlung", Id = collectionId, Title = collectionName, Subtitle = titles, PictureUrl = GetPictureUrl(items.First().CollectionPosterPictureId), LinkUrl = $"/moviecollection/{collectionId}" });
                        continue;
                    }

                    foreach (var movie in items)
                        result.Add(new ActorMediaEntryDto { Type = "Film", Id = movie.Id, Title = movie.Name, PictureUrl = GetPictureUrl(movie.PosterPictureId), LinkUrl = $"/moviecollection/{collectionId}?movie={movie.Id}" });
                }
            }

            var episodeActors = await _db.TVShowEpisodeActors
                .AsNoTracking()
                .Where(ea => ea.ActorId == actorId && allowedSourceIds.Contains(ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId))
                .Select(ea => new
                {
                    ea.TVShowEpisode.Id,
                    ea.TVShowEpisode.Name,
                    ea.TVShowEpisode.PosterPictureId,
                    ea.TVShowEpisode.TVShowSeasonId,
                    SeasonName = ea.TVShowEpisode.TVShowSeason.Name,
                    SeasonPosterPictureId = (long?)ea.TVShowEpisode.TVShowSeason.PosterPictureId,
                    ShowId = ea.TVShowEpisode.TVShowSeason.TVShow.Id,
                    ShowName = ea.TVShowEpisode.TVShowSeason.TVShow.Name,
                    ShowPosterPictureId = (long?)ea.TVShowEpisode.TVShowSeason.TVShow.PosterPictureId
                })
                .ToListAsync(cancellationToken);

            if (episodeActors.Count > 0)
            {
                var seasonIds = episodeActors.Select(e => e.TVShowSeasonId).Distinct().ToList();
                var showIds = episodeActors.Select(e => e.ShowId).Distinct().ToList();

                var seasonTotals = await _db.TVShowEpisodes
                    .AsNoTracking()
                    .Where(e => seasonIds.Contains(e.TVShowSeasonId) && allowedSourceIds.Contains(e.TVShowSeason.TVShow.MediaSourceId))
                    .GroupBy(e => e.TVShowSeasonId)
                    .Select(g => new { SeasonId = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(g => g.SeasonId, g => g.Total, cancellationToken);

                var showTotals = await _db.TVShowEpisodes
                    .AsNoTracking()
                    .Where(e => showIds.Contains(e.TVShowSeason.TVShowId) && allowedSourceIds.Contains(e.TVShowSeason.TVShow.MediaSourceId))
                    .GroupBy(e => e.TVShowSeason.TVShowId)
                    .Select(g => new { ShowId = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(g => g.ShowId, g => g.Total, cancellationToken);

                foreach (var showGroup in episodeActors.GroupBy(e => e.ShowId))
                {
                    var showEpisodes = showGroup.ToList();
                    var showName = showEpisodes.First().ShowName;
                    var showId = showGroup.Key;

                    if (showTotals.GetValueOrDefault(showId) == showEpisodes.Count)
                    {
                        result.Add(new ActorMediaEntryDto { Type = "Serie", Id = showId, Title = showName, Subtitle = $"{showEpisodes.Count} Episoden", PictureUrl = GetPictureUrl(showEpisodes.First().ShowPosterPictureId), LinkUrl = $"/tvshow/{showId}" });
                        continue;
                    }

                    foreach (var seasonGroup in showEpisodes.GroupBy(e => e.TVShowSeasonId))
                    {
                        var seasonName = seasonGroup.First().SeasonName;
                        var seasonEpisodes = seasonGroup.ToList();
                        var seasonId = seasonGroup.Key;

                        if (seasonTotals.GetValueOrDefault(seasonId) == seasonEpisodes.Count)
                        {
                            result.Add(new ActorMediaEntryDto { Type = "Staffel", Id = seasonId, Title = $"{showName} - {seasonName}", Subtitle = $"{seasonEpisodes.Count} Episoden", PictureUrl = GetPictureUrl(seasonEpisodes.First().SeasonPosterPictureId), LinkUrl = $"/tvshow/{showId}?season={seasonId}" });
                            continue;
                        }

                        foreach (var ep in seasonEpisodes)
                            result.Add(new ActorMediaEntryDto { Type = "Episode", Id = ep.Id, Title = ep.Name, Subtitle = $"{showName} - {seasonName}", PictureUrl = GetPictureUrl(ep.PosterPictureId ?? ep.SeasonPosterPictureId ?? ep.ShowPosterPictureId), LinkUrl = $"/tvshow/{showId}?season={seasonId}&episode={ep.Id}" });
                    }
                }
            }

            return result;
        }

        private static string? GetPictureUrl(long? pictureId)
            => pictureId.HasValue ? $"/api/pictures/{pictureId}" : null;
    }
}
