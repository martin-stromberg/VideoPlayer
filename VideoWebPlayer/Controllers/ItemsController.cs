using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

/// <summary>
/// Provides media item discovery and streaming endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[BearerTokenCheck]
public class ItemsController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    private readonly SftpMediaSourceReader _sftpReader;
    private readonly MediaMetadataEditorService _metadataEditor;
    private readonly RecentEntryService recentEntryService;
    private readonly IUnlockedMediaService _unlockedMediaService;
    private readonly WatchedStatusService _watchedStatusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemsController"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="sftpReader">SFTP reader.</param>
    /// <param name="recentEntryService">Recent entry service.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public ItemsController(
        ApplicationDbContext db, 
        SftpMediaSourceReader sftpReader,
        MediaMetadataEditorService metadataEditor,
        RecentEntryService recentEntryService,
        IUnlockedMediaService unlockedMediaService,
        
        IAuthService authService, 
        ILogger<ItemsController> logger,
        WatchedStatusService? watchedStatusService = null) : base(authService, logger)
    {
        _db = db;
        _sftpReader = sftpReader;
        _metadataEditor = metadataEditor;
        this.recentEntryService = recentEntryService;
        _unlockedMediaService = unlockedMediaService;
        _watchedStatusService = watchedStatusService ?? new WatchedStatusService(db);
    }

    /// <summary>
    /// Gets genre options as displayed by the genre admin page.
    /// </summary>
    [HttpGet("genres")]
    public async Task<ActionResult<List<DtoGenreOption>>> GetGenres()
    {
        try
        {
            CheckLogedIn();
            return Ok(await _metadataEditor.GetGenreOptionsAsync(HttpContext.RequestAborted));
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Genre-Auswahlliste");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Genre-Auswahlliste");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates user-editable metadata for a media detail context.
    /// </summary>
    [HttpPost("metadata")]
    public async Task<IActionResult> UpdateMetadata([FromBody] MediaMetadataUpdateRequest request)
    {
        try
        {
            CheckLogedIn();
            if (!User.HasClaim("IsAdmin", "True"))
                return Unauthorized("Nur Administratoren duerfen Metadaten speichern.");

            await _metadataEditor.UpdateAsync(request, HttpContext.RequestAborted);
            return Ok(true);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Ungueltige Metadaten-Aktualisierung");
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Metadaten-Ziel nicht gefunden");
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Speichern von Metadaten");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Speichern von Metadaten");
            return StatusCode(500, "Internal server error");
        }
    }
    /// <summary>
    /// Gets media entries for a source with optional filtering.
    /// </summary>
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
            // MovieCollections
            var queryMovie = _db.MovieCollections
                .AsNoTracking()
                .Where(mc => !mediaSourceId.HasValue || mc.MediaSourceId == mediaSourceId);
            var foundMovies = queryMovie.ToList();

            if (!string.IsNullOrWhiteSpace(search))
                queryMovie = queryMovie.Where(e => e.Name.Contains(search));
            foundMovies = queryMovie.ToList();

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

            var mediaSourceIds = await _db.MediaSourceUsers
                .AsNoTracking()
                .Where(msu => msu.UserId == CurrentUser.Id)
                .Select(msu => msu.MediaSourceId)
                .ToArrayAsync();

            var unlockedMovieCollectionIds = await _unlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync(CurrentUser.Id);
            var unlockedTVShowIds = await _unlockedMediaService.GetUnlockedTVShowIdsForUserAsync(CurrentUser.Id);

            var movieCollections = (await queryMovie
                .Where(m => mediaSourceIds.Contains(m.MediaSourceId) || unlockedMovieCollectionIds.Contains(m.Id))
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
                .AsNoTracking()
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
                .Where(m => mediaSourceIds.Contains(m.MediaSourceId) || unlockedTVShowIds.Contains(m.Id))
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
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Genres fuer Quelle");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Genres fuer Quelle");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets recently watched media entries.
    /// </summary>
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
            await _watchedStatusService.EnrichAsync(CurrentUser.Id, dtoList.Select(x => x.Entry), RequestCancellationToken);
            return Ok(dtoList);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der letzten Eintraege");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der letzten Eintraege");
            return StatusCode(500, "Internal server error");
        }
    }
    
    private async Task<MediaBaseEntry> FindEntry(string type, long id)
    {
        var entry = type switch
        {
            "movie" => await _db.Movies.AsNoTracking()
                .FirstOrDefaultAsync(mc => mc.Id == id) as MediaBaseEntry,
            "moviecollection" => await _db.MovieCollections.AsNoTracking().FirstOrDefaultAsync(mc => mc.Id == id) as MediaBaseEntry,
            "tvshow" => await _db.TVShows.AsNoTracking()
                .FirstOrDefaultAsync(ts => ts.Id == id) as MediaBaseEntry,
            "tvshowseason" => await _db.TVShowSeasons.AsNoTracking().FirstOrDefaultAsync(ts => ts.Id == id) as MediaBaseEntry,
            "tvshowepisode" => await _db.TVShowEpisodes.AsNoTracking().FirstOrDefaultAsync(tse => tse.Id == id) as MediaBaseEntry,
            _ => null
        };
        if (entry == null)
            throw new RecordNotFoundException("Medieneintrag nicht gefunden");
        return entry;
    }

    private async Task EnsureAccessAsync(MediaBaseEntry entry)
    {
        var source = await _db.MediaSources
            .AsNoTracking()
            .FirstOrDefaultAsync(ms => ms.Id == entry.MediaSourceId);
        if (source is null)
            throw new RecordNotFoundException("Medienquelle nicht gefunden");

        var hasSourceAccess = await _db.MediaSourceUsers.AnyAsync(u => u.UserId == CurrentUser.Id && u.MediaSourceId == source.Id);
        var isUnlocked = await IsUnlockedAsync(entry);
        if (!hasSourceAccess && !isUnlocked)
            throw new UnauthorizedAccessException("Fehlende Berechtigung fuer Medienquelle");
    }

    private async Task<bool> IsUnlockedAsync(MediaBaseEntry entry)
    {
        if (entry is Movie movie)
            return await _db.UnlockedMediaEntries.AsNoTracking().AnyAsync(u => u.UserId == CurrentUser.Id && u.MovieCollectionId == movie.MovieCollectionId);

        if (entry is TVShowEpisode episode)
        {
            var showId = await _db.TVShowSeasons.AsNoTracking()
                .Where(s => s.Id == episode.TVShowSeasonId)
                .Select(s => (long?)s.TVShowId)
                .FirstOrDefaultAsync();
            return await _db.UnlockedMediaEntries.AsNoTracking().AnyAsync(u => u.UserId == CurrentUser.Id && u.TVShowId == showId);
        }

        return await _db.UnlockedMediaEntries.AsNoTracking().AnyAsync(u => u.UserId == CurrentUser.Id && (u.MovieCollectionId == entry.Id || u.TVShowId == entry.Id));
    }

    private CancellationToken RequestCancellationToken => HttpContext?.RequestAborted ?? CancellationToken.None;

    private async Task<MediaItem> FindMediaItemAsync(string type, long id)
    {
        var entry = await FindEntry(type, id);
        await EnsureAccessAsync(entry);

        var mediaItem = type switch
        {
            "movie" => await _db.MovieMediaItems
                .Include(mi => mi.MediaItem)
                .Where(mi => mi.MovieId == entry.Id)
                .Select(mi => mi.MediaItem)
                .FirstOrDefaultAsync(),
            "tvshowepisode" => await _db.TVShowEpisodeMediaItems
                .Include(mi => mi.MediaItem)
                .Where(mi => mi.TVShowEpisodeId == entry.Id)
                .Select(mi => mi.MediaItem)
                .FirstOrDefaultAsync(),
            _ => null
        };
        if (mediaItem is null)
            throw new RecordNotFoundException("Keine Medienitems fuer diesen Eintrag gefunden");
        return mediaItem;
    }

    /// <summary>
    /// Streams a media item by type and identifier.
    /// </summary>
    [HttpGet("{type}/{id}/stream")]
    public async Task<IActionResult> StreamMediaItem(string type, long id)
    {
        try
        {
            CheckLogedIn();
            if (type == nameof(TVShow).ToLower())
                type = nameof(TVShowEpisode).ToLower(); 

            if (type != nameof(Movie).ToLower() && type != nameof(TVShowEpisode).ToLower())
                return BadRequest("Ungueltiger Medientyp");
            if (id <= 0)
                return BadRequest("Ungueltige ID");

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

            // enableRangeProcessing: true for video streaming
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

    /// <summary>
    /// Downloads a media item by type and identifier.
    /// </summary>
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

            // Optional: read file name
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

    /// <summary>
    /// Gets media details for a movie collection or TV show.
    /// </summary>
    [HttpGet("{type}/{id}")]
    public async Task<IActionResult> Get(string type, long id)
    {
        try
        {
            CheckLogedIn();
            var entry = await FindEntry(type, id);
            await EnsureAccessAsync(entry);
            if (entry is MovieCollection)
            {
                var collection = Create<DtoMovieCollection>(entry);
                collection.IsUnlocked = await _unlockedMediaService.IsUnlockedAsync(collection);
                collection.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.MovieCollectionId == collection.Id);
                collection.Movies = _db.Movies.Where(m => m.MovieCollectionId == collection.Id).ToList().Select(m =>
                {
                    var movie = Create<DtoMovie>(m);
                    movie.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.MovieId == movie.Id);
                    return movie;
                }).ToArray();
                await _watchedStatusService.EnrichAsync(CurrentUser.Id, collection.Movies, RequestCancellationToken);
                return Ok(collection);
            }
            else if (entry is TVShow)
            {
                var show = Create<DtoTVShow>(entry);
                show.IsUnlocked = await _unlockedMediaService.IsUnlockedAsync(show);
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
                await _watchedStatusService.EnrichAsync(CurrentUser.Id, show.Seasons.SelectMany(s => s.Episodes), RequestCancellationToken);
                return Ok(show);
            }
            else if (entry is TVShowEpisode dbSeason)
            {
                var episode = Create<DtoTVShowEpisode>(entry);
                episode.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.TVShowEpisodeId == episode.Id);
                await _watchedStatusService.EnrichAsync(CurrentUser.Id, [episode], RequestCancellationToken);
                episode.Season = _db.TVShowSeasons.Where(m => m.Id == dbSeason.TVShowSeasonId).ToList().Select(m =>
                {
                    var season = Create<DtoTVShowSeason>(m);
                    season.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.TVShowSeasonId == season.Id);                    
                    season.Show = _db.TVShows.Where(s => s.Id == m.TVShowId).ToList().Select(s =>
                    {
                        var show = Create<DtoTVShow>(s);
                        show.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.TVShowId == show.Id);
                        return show;
                    }).FirstOrDefault();
                    return season;
                }).First();
                return Ok(episode);
            }
            else if (entry is Movie dbMovie)
            {
                var movie = Create<DtoMovie>(dbMovie);
                movie.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.MovieId == movie.Id);
                await _watchedStatusService.EnrichAsync(CurrentUser.Id, [movie], RequestCancellationToken);
                movie.Collection = _db.MovieCollections.Where(mc => mc.Id == dbMovie.MovieCollectionId).ToList().Select(mc =>
                {
                    var collection = Create<DtoMovieCollection>(mc);
                    collection.IsFavorite = _db.FavoriteEntries.Any(f => f.UserId == CurrentUser.Id && f.MovieCollectionId == collection.Id);
                    return collection;
                }).FirstOrDefault();
                return Ok(movie);
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


