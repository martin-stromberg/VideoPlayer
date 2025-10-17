using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class ItemsController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    private readonly SftpMediaSourceReader _sftpReader;
    private readonly RecentEntryService recentEntryService;

    public ItemsController(
        ApplicationDbContext db, 
        SftpMediaSourceReader sftpReader,
        RecentEntryService recentEntryService,
        
        IAuthService authService, 
        ILogger<ItemsController> logger) : base(authService, logger)
    {
        _db = db;
        _sftpReader = sftpReader;
        this.recentEntryService = recentEntryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MediaEntryDto>>> Get(
            [FromQuery] long? mediaSourceId,
            [FromQuery] int page = 0,
            [FromQuery] int size = 30,
            [FromQuery] string? search = null,
            [FromQuery] long? genreId = null)
    {
        try
        {
            CheckLogedIn();
            // Genres für die Buttons: Nur Genres ohne Alternativnamen
            var genreButtonList = await _db.Genres
                .Include(g => g.AlternateNames)
                .Where(g => (!mediaSourceId.HasValue || g.MediaSourceId == mediaSourceId) && !g.AlternateNames.Any())
                .OrderBy(g => g.Name)
                .ToListAsync();

            // MovieCollections
            var queryMovie = _db.MovieCollections
                .Where(mc => !mediaSourceId.HasValue || mc.MediaSourceId == mediaSourceId);

            if (!string.IsNullOrWhiteSpace(search))
                queryMovie = queryMovie.Where(e => e.Name.Contains(search));

            if (genreId.HasValue)
            {
                // Nur Collections, deren Movies das Genre haben
                queryMovie = queryMovie.Where(mc =>
                    _db.Movies.Any(m =>
                        m.MovieCollectionId == mc.Id &&
                        m.MovieGenres.Any(mg => mg.GenreId == genreId.Value)
                    )
                );
            }
            var mediaSourceIds = await _db.MediaSourceUsers.Where(msu => msu.UserId == CurrentUser.Id).Select(msu => msu.MediaSourceId).ToArrayAsync();

            var movieCollections = (await queryMovie
                .Where(m => mediaSourceIds.Contains(m.MediaSourceId))
                .OrderBy(e => e.Name)
                .Skip(0)
                .Take((page + 1) * size)
                .Select(mc => new MediaEntryDto
                {
                    Type = nameof(Movie),
                    Id = mc.Id,
                    Title = mc.Name,
                    Description = "",
                    Url = $"/moviecollection/{mc.Id}",
                    CreatedAt = mc.CreatedAt,
                    PictureId = mc.PosterPictureId,
                    ItemCount = _db.Movies.Count(m => m.MovieCollectionId == mc.Id)
                })
                .ToListAsync());

            // TVShows
            var queryShow = _db.TVShows
                .Where(ts => !mediaSourceId.HasValue || ts.MediaSourceId == mediaSourceId);

            if (!string.IsNullOrWhiteSpace(search))
                queryShow = queryShow.Where(e => e.Name.Contains(search));

            if (genreId.HasValue)
            {
                queryShow = queryShow.Where(ts =>
                    ts.TVShowGenres.Any(tg => tg.GenreId == genreId.Value)
                );
            }

            var tvShows = await queryShow
                .Where(m => mediaSourceIds.Contains(m.MediaSourceId))
                .OrderBy(e => e.Name)
                .Skip(0)
                .Take((page + 1) * size)
                .Select(ts => new MediaEntryDto
                {
                    Type = nameof(TVShow),
                    Id = ts.Id,
                    Title = ts.Name,
                    Description = ts.Plot,
                    Url = $"/tvshow/{ts.Id}",
                    CreatedAt = ts.CreatedAt,
                    PictureId = ts.PosterPictureId
                })
                .ToListAsync();

            var entries = movieCollections
                .Concat(tvShows)
                .OrderBy(e => e.Title)
                .Skip(page * size)
                .Take(size)
                .ToList();

            return Ok(entries);
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

    [HttpGet("recent")]
    public async Task<ActionResult<List<DtoRecentEntry>>> GetRecent()
    {
        try
        {
            CheckLogedIn();
            var recent = await recentEntryService.GetRecentEntriesAsync();
            var dtoList = new List<DtoRecentEntry>();
            foreach (var rec in recent)
            {
                try
                {
                    var dto = Create<DtoRecentEntry>(rec);
                    switch (rec.Type)
                    {
                        case RecentEntryType.Movie:
                            dto.Entry = _db.Movies
                                .Where(m => m.Id == rec.MovieId)
                                .ToList()
                                .Select(m =>
                                {
                                    var movie = Create<DtoMovie>(m);
                                    movie.Collection = _db.MovieCollections
                                        .Where(mc => mc.Id == m.MovieCollectionId)
                                        .ToList()
                                        .Select(mc => Create<DtoMovieCollection>(mc))
                                        .FirstOrDefault();
                                    return movie;
                                })
                                .FirstOrDefault();
                            break;
                        case RecentEntryType.MovieCollection:
                            dto.Entry = _db.MovieCollections
                                .Where(mc => mc.Id == rec.MovieCollectionId)
                                .ToList()
                                .Select(mc => Create<DtoMovieCollection>(mc))
                                .FirstOrDefault();
                            break;
                        case RecentEntryType.TVShow:
                            dto.Entry = _db.TVShows
                                .Where(ts => ts.Id == rec.TVShowId)
                                .ToList()
                                .Select(ts => Create<DtoTVShow>(ts))
                                .FirstOrDefault();
                            break;
                        case RecentEntryType.TVShowSeason:
                            dto.Entry = _db.TVShowSeasons
                                .Where(ts => ts.Id == rec.TVShowSeasonId)
                                .ToList()
                                .Select(ts =>
                                {
                                    var season = Create<DtoTVShowSeason>(ts);
                                    season.Show = _db.TVShows
                                        .Where(s => s.Id == ts.TVShowId)
                                        .ToList()
                                        .Select(s => Create<DtoTVShow>(s))
                                        .FirstOrDefault();
                                    return season;
                                })
                                .FirstOrDefault();
                            break;
                        case RecentEntryType.TVShowEpisode:
                            dto.Entry = _db.TVShowEpisodes
                                .Where(tse => tse.Id == rec.TVShowEpisodeId)
                                .ToList()
                                .Select(tse =>
                                {
                                    var episode = Create<DtoTVShowEpisode>(tse);
                                    episode.Season = _db.TVShowSeasons
                                        .Where(ts => ts.Id == tse.TVShowSeasonId)
                                        .ToList()
                                        .Select(ts =>
                                        {
                                            var season = Create<DtoTVShowSeason>(ts);
                                            season.Show = _db.TVShows
                                                .Where(s => s.Id == ts.TVShowId)
                                                .Select(s => Create<DtoTVShow>(s))
                                                .FirstOrDefault();
                                            return season;
                                        })
                                        .FirstOrDefault();
                                    return episode;
                                })
                                .FirstOrDefault();
                            break;
                    }
                    if (dto.Entry != null)
                        dtoList.Add(dto);
                }
                catch (Exception innerEx)
                {
                    Logger.LogWarning(innerEx, "Fehler beim Verarbeiten eines RecentEntry (Id={Id})", rec.Id);
                }
            }
            return Ok(dtoList);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der letzten Einträge");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der letzten Einträge");
            return StatusCode(500, "Internal server error");
        }
    }
    
    private async Task<MediaBaseEntry> FindEntry(string type, long id)
    {
        var entry = type switch
        {
            "movie" => await _db.Movies
                .FirstOrDefaultAsync(mc => mc.Id == id) as MediaBaseEntry,
            "moviecollection" => await _db.MovieCollections.FirstOrDefaultAsync(mc => mc.Id == id) as MediaBaseEntry,
            "tvshow" => await _db.TVShows
                .FirstOrDefaultAsync(ts => ts.Id == id) as MediaBaseEntry,
            "tvshowseason" => await _db.TVShowSeasons.FirstOrDefaultAsync(ts => ts.Id == id) as MediaBaseEntry,
            "tvshowepisode" => await _db.TVShowEpisodes.FirstOrDefaultAsync(tse => tse.Id == id) as MediaBaseEntry,
            _ => null
        };
        if (entry == null)
            throw new RecordNotFoundException("Medieneintrag nicht gefunden");
        return entry;
    }
    private async Task<MediaItem> FindMediaItemAsync(string type, long id)
    {
        var entry = await FindEntry(type, id);

        var source = await _db.MediaSources
            .Include(ms => ms.MediaSourceUsers)
            .FirstOrDefaultAsync(ms => ms.Id == entry.MediaSourceId);
        if (source is null)
            throw new RecordNotFoundException("Medienquelle nicht gefunden");
        if (!source.MediaSourceUsers.Any(u => u.UserId == CurrentUser.Id))
            throw new UnauthorizedAccessException("Fehlende Berechtigung für Medienquelle");

        var mediaItems = type switch
        {
            "movie" => await _db.MovieMediaItems
                .Include(mi => mi.MediaItem)
                .Where(mi => mi.MovieId == entry.Id)
                .Select(mi => mi.MediaItem)
                .ToListAsync(),
            "tvshowepisode" => await _db.TVShowEpisodeMediaItems
                .Include(mi => mi.MediaItem)
                .Where(mi => mi.TVShowEpisodeId == entry.Id)
                .Select(mi => mi.MediaItem)
                .ToListAsync(),
            _ => new List<MediaItem>()
        };
        if (mediaItems.Count == 0)
            throw new RecordNotFoundException("Keine Medienitems für diesen Eintrag gefunden");
        return mediaItems.FirstOrDefault();
    }

    [HttpGet("{type}/{id}/stream")]
    public async Task<IActionResult> StreamMediaItem(string type, long id)
    {
        try
        {
            CheckLogedIn();
            if (type == nameof(TVShow).ToLower())
                type = nameof(TVShowEpisode).ToLower(); 

            if (type != nameof(Movie).ToLower() && type != nameof(TVShowEpisode).ToLower())
                return BadRequest("Ungültiger Medientyp");
            if (id <= 0)
                return BadRequest("Ungültige ID");

            var mediaItem = await FindMediaItemAsync(type, id);
            if (mediaItem == null)
                return NotFound();

            var mediaCollection = await _db.MediaCollections
                .Include(mc => mc.MediaSource)
                .FirstOrDefaultAsync(mc => mc.Id == mediaItem.MediaCollectionId);

            var fileName = Path.GetFileName(mediaItem.Path);
            var stream = _sftpReader.GetSftpFileStream(mediaCollection, fileName);
            if (stream == null)
                return NotFound();

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".mp4" => "video/mp4",
                ".mkv" => "video/x-matroska",
                ".avi" => "video/x-msvideo",
                ".mpeg" => "video/mpeg",
                _ => "application/octet-stream"
            };

            // enableRangeProcessing: true für Video-Streaming
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Streamen des Medienitems");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Streamen des Medienitems");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{type}/{id}/download")]
    public async Task<IActionResult> Download(string type, long id)
    {
        try
        {
            CheckLogedIn();

            var mediaItem = await FindMediaItemAsync(type, id);
            if (mediaItem == null)
                return NotFound();

            var fileStreamResult = await StreamMediaItem(type, id) as FileStreamResult;
            if (fileStreamResult == null)
                return NotFound();

            // Optional: Dateiname auslesen
            var fileName = !string.IsNullOrWhiteSpace(fileStreamResult.FileDownloadName) ? fileStreamResult.FileDownloadName : Path.GetFileName(mediaItem.Path) ?? $"video_{mediaItem.Id}.mp4";
            return File(fileStreamResult.FileStream, "application/octet-stream", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Streamen des Medienitems");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Streamen des Medienitems");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{type}/{id}")]
    public async Task<IActionResult> Get(string type, long id)
    {
        try
        {
            CheckLogedIn();
            var entry = await FindEntry(type, id);
            if (entry is MovieCollection)
            {
                var collection = Create<DtoMovieCollection>(entry);
                collection.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.MovieCollectionId == collection.Id);
                collection.Movies = _db.Movies.Where(m => m.MovieCollectionId == collection.Id).ToList().Select(m =>
                {
                    var movie = Create<DtoMovie>(m);
                    movie.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.MovieId == movie.Id);
                    return movie;
                }).ToArray();
                return Ok(collection);
            }
            else if (entry is TVShow)
            {
                var show = Create<DtoTVShow>(entry);
                show.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.TVShowId == show.Id);
                show.Seasons = _db.TVShowSeasons.Where(m => m.TVShowId == show.Id).ToList().Select(m =>
                {
                    var season = Create<DtoTVShowSeason>(m);
                    season.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.TVShowSeasonId == season.Id);
                    season.Episodes = _db.TVShowEpisodes.Where(e => e.TVShowSeasonId == season.Id).ToList().Select(e =>
                    {
                        var episode = Create<DtoTVShowEpisode>(e);
                        episode.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.TVShowEpisodeId == episode.Id);
                        return episode;
                    }).ToArray();
                    return season;
                }).ToArray();
                return Ok(show);
            }
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen des Medienitems");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen des Medienitems");
            return StatusCode(500, "Internal server error");
        }
    }

}


