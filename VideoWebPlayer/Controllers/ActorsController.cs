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
        /// Gets actors, optionally filtered by search term and filter, paged by offset and limit.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetActors([FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] string? filter = null, [FromQuery] int offset = 0, [FromQuery] int limit = 50)
        {
            try
            {
                CheckLoggedIn();

                offset = Math.Max(0, offset);
                limit = Math.Clamp(limit, 1, 200);

                var allowedSourceIds = await GetAllowedSourceIdsAsync();
                if (allowedSourceIds.Count == 0)
                    return Ok(Array.Empty<ActorDto>());

                var setup = await _db.Setups.AsNoTracking().FirstOrDefaultAsync();
                var threshold = (setup?.ActorCollectionThresholdPercent ?? 50) / 100.0;

                var actorVideoCounts = await GetActorAggregatedVideoCountsAsync(allowedSourceIds, threshold);

                var baseQuery = _db.Actors
                    .AsNoTracking()
                    .Where(a => _db.MovieActors.Any(ma => ma.ActorId == a.Id && allowedSourceIds.Contains(ma.Movie.MediaSourceId)) ||
                        _db.TVShowEpisodeActors.Any(ea => ea.ActorId == a.Id && allowedSourceIds.Contains(ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId)));

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim().ToUpperInvariant();
                    baseQuery = baseQuery.Where(a => a.NormalizedName.Contains(term));
                }

                var isCountSort = string.Equals(sort, "count", StringComparison.OrdinalIgnoreCase);

                if (isCountSort)
                {
                    var actors = await baseQuery
                        .Select(a => new { a.Id, a.Name, a.NormalizedName, a.PictureId })
                        .ToListAsync();

                    var withCount = actors
                        .Select(a => new { a.Id, a.Name, a.NormalizedName, a.PictureId, Count = actorVideoCounts.GetValueOrDefault(a.Id) });

                    if (!string.IsNullOrWhiteSpace(filter))
                    {
                        var bucket = CountBuckets.FirstOrDefault(b => b.Label == filter);
                        if (bucket is not null)
                        {
                            withCount = withCount.Where(a =>
                                a.Count >= bucket.Min &&
                                (!bucket.Max.HasValue || a.Count <= bucket.Max.Value));
                        }
                    }

                    var paged = withCount
                        .OrderByDescending(a => a.Count)
                        .ThenBy(a => a.NormalizedName)
                        .ThenBy(a => a.Id)
                        .Skip(offset)
                        .Take(limit)
                        .ToList();

                    var result = paged
                        .Select(a => new ActorDto
                        {
                            Id = a.Id,
                            Name = a.Name,
                            PictureUrl = a.PictureId.HasValue ? $"/api/pictures/{a.PictureId}" : null,
                            VideoCount = a.Count
                        })
                        .ToList();

                    return Ok(result);
                }

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var letter = filter.ToUpperInvariant()[0];
                    baseQuery = baseQuery.Where(a => a.NormalizedName.StartsWith(letter.ToString(), StringComparison.OrdinalIgnoreCase));
                }

                var pagedActors = await baseQuery
                    .OrderBy(a => a.NormalizedName)
                    .ThenBy(a => a.Id)
                    .Skip(offset)
                    .Take(limit)
                    .Select(a => new { a.Id, a.Name, a.NormalizedName, a.PictureId })
                    .ToListAsync();

                var resultActors = pagedActors
                    .Select(a => new ActorDto
                    {
                        Id = a.Id,
                        Name = a.Name,
                        PictureUrl = a.PictureId.HasValue ? $"/api/pictures/{a.PictureId}" : null,
                        VideoCount = actorVideoCounts.GetValueOrDefault(a.Id)
                    })
                    .ToList();

                return Ok(resultActors);
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
        /// Gets actors for a specific movie.
        /// </summary>
        [HttpGet("by-movie/{movieId:long}")]
        public async Task<IActionResult> GetActorsForMovie(long movieId)
        {
            try
            {
                CheckLoggedIn();

                var allowedSourceIds = await GetAllowedSourceIdsAsync();

                var movie = await _db.Movies
                    .AsNoTracking()
                    .Select(m => new { m.Id, m.MediaSourceId, m.MovieCollectionId })
                    .FirstOrDefaultAsync(m => m.Id == movieId);

                if (movie is null)
                    return NotFound("Film nicht gefunden.");

                if (!allowedSourceIds.Contains(movie.MediaSourceId))
                    return Ok(Array.Empty<ActorDto>());

                var actorLinks = await _db.MovieActors
                    .AsNoTracking()
                    .Where(ma => ma.MovieId == movieId)
                    .Select(ma => new { ma.ActorId, ma.Actor.Name, ma.Actor.PictureId, ma.Role, ma.Order })
                    .ToListAsync();

                var actorIds = actorLinks.Select(a => a.ActorId).ToList();

                var contextCounts = movie.MovieCollectionId.HasValue
                    ? await _db.MovieActors
                        .AsNoTracking()
                        .Where(ma => actorIds.Contains(ma.ActorId) && ma.Movie.MovieCollectionId == movie.MovieCollectionId.Value && allowedSourceIds.Contains(ma.Movie.MediaSourceId))
                        .GroupBy(ma => ma.ActorId)
                        .Select(g => new { ActorId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.ActorId, x => x.Count)
                    : new Dictionary<long, int>();

                var actors = actorLinks
                    .Select(a => new ActorDto
                    {
                        Id = a.ActorId,
                        Name = a.Name,
                        PictureUrl = a.PictureId.HasValue ? $"/api/pictures/{a.PictureId}" : null,
                        Role = a.Role,
                        Order = a.Order,
                        ContextVideoCount = contextCounts.GetValueOrDefault(a.ActorId, 1)
                    })
                    .ToList();

                return Ok(actors);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Schauspieler eines Films");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Abrufen der Schauspieler eines Films");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets actors for a specific episode.
        /// </summary>
        [HttpGet("by-episode/{episodeId:long}")]
        public async Task<IActionResult> GetActorsForEpisode(long episodeId)
        {
            try
            {
                CheckLoggedIn();

                var allowedSourceIds = await GetAllowedSourceIdsAsync();

                var episode = await _db.TVShowEpisodes
                    .AsNoTracking()
                    .Select(e => new { e.Id, e.TVShowSeasonId, TVShowId = e.TVShowSeason.TVShowId, e.TVShowSeason.TVShow.MediaSourceId })
                    .FirstOrDefaultAsync(e => e.Id == episodeId);

                if (episode is null)
                    return NotFound("Episode nicht gefunden.");

                if (!allowedSourceIds.Contains(episode.MediaSourceId))
                    return Ok(Array.Empty<ActorDto>());

                var actorLinks = await _db.TVShowEpisodeActors
                    .AsNoTracking()
                    .Where(ea => ea.TVShowEpisodeId == episodeId)
                    .Select(ea => new { ea.ActorId, ea.Actor.Name, ea.Actor.PictureId, ea.Role, ea.Order })
                    .ToListAsync();

                var actorIds = actorLinks.Select(a => a.ActorId).ToList();

                var contextCounts = await _db.TVShowEpisodeActors
                    .AsNoTracking()
                    .Where(ea => actorIds.Contains(ea.ActorId) && ea.TVShowEpisode.TVShowSeason.TVShowId == episode.TVShowId && allowedSourceIds.Contains(ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId))
                    .GroupBy(ea => ea.ActorId)
                    .Select(g => new { ActorId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ActorId, x => x.Count);

                var actors = actorLinks
                    .Select(a => new ActorDto
                    {
                        Id = a.ActorId,
                        Name = a.Name,
                        PictureUrl = a.PictureId.HasValue ? $"/api/pictures/{a.PictureId}" : null,
                        Role = a.Role,
                        Order = a.Order,
                        ContextVideoCount = contextCounts.GetValueOrDefault(a.ActorId, 1)
                    })
                    .ToList();

                return Ok(actors);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Schauspieler einer Episode");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Abrufen der Schauspieler einer Episode");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets filter options for the current actor sort mode.
        /// </summary>
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters([FromQuery] string? sort = null)
        {
            try
            {
                CheckLoggedIn();

                var allowedSourceIds = await GetAllowedSourceIdsAsync();
                if (allowedSourceIds.Count == 0)
                    return Ok(Array.Empty<string>());

                if (string.Equals(sort, "count", StringComparison.OrdinalIgnoreCase))
                {
                    var setup = await _db.Setups.AsNoTracking().FirstOrDefaultAsync();
                    var threshold = (setup?.ActorCollectionThresholdPercent ?? 50) / 100.0;
                    var actorVideoCounts = await GetActorAggregatedVideoCountsAsync(allowedSourceIds, threshold);
                    var usedBuckets = CountBuckets
                        .Where(b => actorVideoCounts.Values.Any(c => c >= b.Min && (!b.Max.HasValue || c <= b.Max.Value)))
                        .Select(b => b.Label)
                        .ToList();
                    return Ok(usedBuckets);
                }

                var initials = await _db.Actors
                    .AsNoTracking()
                    .Where(a => _db.MovieActors.Any(ma => ma.ActorId == a.Id && allowedSourceIds.Contains(ma.Movie.MediaSourceId)) ||
                        _db.TVShowEpisodeActors.Any(ea => ea.ActorId == a.Id && allowedSourceIds.Contains(ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId)))
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

                    var showTotal = showTotals.GetValueOrDefault(showId);
                    if (showTotal > 0 && (double)showEpisodes.Count / showTotal >= threshold)
                    {
                        result.Add(new ActorMediaEntryDto { Type = "Serie", Id = showId, Title = showName, Subtitle = $"{showEpisodes.Count} Episoden", PictureUrl = GetPictureUrl(showEpisodes.First().ShowPosterPictureId), LinkUrl = $"/tvshow/{showId}" });
                        continue;
                    }

                    foreach (var seasonGroup in showEpisodes.GroupBy(e => e.TVShowSeasonId))
                    {
                        var seasonName = seasonGroup.First().SeasonName;
                        var seasonEpisodes = seasonGroup.ToList();
                        var seasonId = seasonGroup.Key;
                        var seasonTotal = seasonTotals.GetValueOrDefault(seasonId);

                        if (seasonTotal > 0 && (double)seasonEpisodes.Count / seasonTotal >= threshold)
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

        private async Task<Dictionary<long, int>> GetActorAggregatedVideoCountsAsync(List<long> allowedSourceIds, double threshold, CancellationToken cancellationToken = default)
        {
            var counts = new Dictionary<long, int>();

            var movieActors = await _db.MovieActors
                .AsNoTracking()
                .Where(ma => allowedSourceIds.Contains(ma.Movie.MediaSourceId))
                .Select(ma => new { ma.ActorId, CollectionId = (long?)ma.Movie.MovieCollectionId })
                .ToListAsync(cancellationToken);

            var allCollectionIds = movieActors
                .Where(m => m.CollectionId.HasValue)
                .Select(m => m.CollectionId!.Value)
                .Distinct()
                .ToList();

            var allCollectionTotals = allCollectionIds.Count > 0
                ? await _db.Movies
                    .AsNoTracking()
                    .Where(m => m.MovieCollectionId.HasValue && allCollectionIds.Contains(m.MovieCollectionId.Value) && allowedSourceIds.Contains(m.MediaSourceId))
                    .GroupBy(m => m.MovieCollectionId!.Value)
                    .Select(g => new { CollectionId = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(g => g.CollectionId, g => g.Total, cancellationToken)
                : new Dictionary<long, int>();

            foreach (var actorGroup in movieActors.GroupBy(ma => ma.ActorId))
            {
                var result = 0;
                foreach (var group in actorGroup.GroupBy(m => m.CollectionId))
                {
                    var items = group.ToList();
                    if (!group.Key.HasValue)
                    {
                        result += items.Count;
                        continue;
                    }

                    var collectionId = group.Key.Value;
                    var totalMovies = allCollectionTotals.GetValueOrDefault(collectionId);
                    var actorCount = items.Count;

                    if (totalMovies == 1 || actorCount == 1)
                    {
                        result += actorCount;
                        continue;
                    }

                    if (actorCount == totalMovies)
                    {
                        result += 1;
                        continue;
                    }

                    if (totalMovies > 0 && (double)actorCount / totalMovies >= threshold)
                    {
                        result += 1;
                        continue;
                    }

                    result += actorCount;
                }

                counts[actorGroup.Key] = result;
            }

            var episodeActors = await _db.TVShowEpisodeActors
                .AsNoTracking()
                .Where(ea => allowedSourceIds.Contains(ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId))
                .Select(ea => new { ea.ActorId, ea.TVShowEpisode.TVShowSeasonId, ShowId = ea.TVShowEpisode.TVShowSeason.TVShow.Id })
                .ToListAsync(cancellationToken);

            var allSeasonIds = episodeActors.Select(e => e.TVShowSeasonId).Distinct().ToList();
            var allShowIds = episodeActors.Select(e => e.ShowId).Distinct().ToList();

            var allSeasonTotals = allSeasonIds.Count > 0
                ? await _db.TVShowEpisodes
                    .AsNoTracking()
                    .Where(e => allSeasonIds.Contains(e.TVShowSeasonId) && allowedSourceIds.Contains(e.TVShowSeason.TVShow.MediaSourceId))
                    .GroupBy(e => e.TVShowSeasonId)
                    .Select(g => new { SeasonId = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(g => g.SeasonId, g => g.Total, cancellationToken)
                : new Dictionary<long, int>();

            var allShowTotals = allShowIds.Count > 0
                ? await _db.TVShowEpisodes
                    .AsNoTracking()
                    .Where(e => allShowIds.Contains(e.TVShowSeason.TVShowId) && allowedSourceIds.Contains(e.TVShowSeason.TVShow.MediaSourceId))
                    .GroupBy(e => e.TVShowSeason.TVShowId)
                    .Select(g => new { ShowId = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(g => g.ShowId, g => g.Total, cancellationToken)
                : new Dictionary<long, int>();

            foreach (var actorGroup in episodeActors.GroupBy(ea => ea.ActorId))
            {
                var result = 0;
                foreach (var showGroup in actorGroup.GroupBy(e => e.ShowId))
                {
                    var showId = showGroup.Key;
                    var showEpisodes = showGroup.ToList();
                    var showTotal = allShowTotals.GetValueOrDefault(showId);

                    if (showTotal > 0 && (double)showEpisodes.Count / showTotal >= threshold)
                    {
                        result += 1;
                        continue;
                    }

                    foreach (var seasonGroup in showEpisodes.GroupBy(e => e.TVShowSeasonId))
                    {
                        var seasonId = seasonGroup.Key;
                        var seasonEpisodes = seasonGroup.ToList();
                        var seasonTotal = allSeasonTotals.GetValueOrDefault(seasonId);

                        if (seasonTotal > 0 && (double)seasonEpisodes.Count / seasonTotal >= threshold)
                        {
                            result += 1;
                            continue;
                        }

                        result += seasonEpisodes.Count;
                    }
                }

                if (counts.ContainsKey(actorGroup.Key))
                    counts[actorGroup.Key] += result;
                else
                    counts[actorGroup.Key] = result;
            }

            return counts;
        }

        private record CountBucket(int Min, int? Max, string Label);

        private static readonly CountBucket[] CountBuckets = new[]
        {
            new CountBucket(1, 1, "1"),
            new CountBucket(2, 5, "2-5"),
            new CountBucket(6, 10, "6-10"),
            new CountBucket(11, null, "11+")
        };

        private static string? GetPictureUrl(long? pictureId)
            => pictureId.HasValue ? $"/api/pictures/{pictureId}" : null;
    }
}
