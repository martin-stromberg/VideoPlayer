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
    /// <param name="metadataEditor">Media metadata editor service.</param>
    /// <param name="recentEntryService">Recent entry service.</param>
    /// <param name="unlockedMediaService">Unlocked-media authorization service.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="watchedStatusService">Watched-status service.</param>
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
    /// <param name="mediaSourceId">Optional media source id to restrict results to (media-source browsing use case).</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="size">Page size.</param>
    /// <param name="search">Optional case-insensitive substring to match against the entry name.</param>
    /// <param name="genreId">Optional genre id to restrict results to.</param>
    /// <param name="includeIndividualMediaTypes">
    /// When <c>true</c>, individual <c>Movie</c>, <c>TVShowSeason</c> and <c>TVShowEpisode</c> entries are
    /// included in addition to <c>MovieCollection</c> and <c>TVShow</c> entries. Used by the playlist media
    /// search (<see cref="VideoWebPlayer.Components.Playlists.MediaSearchSelector"/>); media-source browsing
    /// leaves this at its default (<c>false</c>) to keep returning only the original two media types.
    /// </param>
    [HttpGet]
    public async Task<ActionResult<List<MediaEntryDto>>> Get(
            [FromQuery] long? mediaSourceId,
            [FromQuery] int page = 0,
            [FromQuery] int size = 30,
            [FromQuery] string? search = null,
            [FromQuery] long? genreId = null,
            [FromQuery] bool includeIndividualMediaTypes = false)
    {
        try
        {
            CheckLogedIn();

            var mediaSourceIds = await _db.MediaSourceUsers
                .AsNoTracking()
                .Where(msu => msu.UserId == CurrentUser.Id)
                .Select(msu => msu.MediaSourceId)
                .ToArrayAsync();

            var unlockedMovieCollectionIds = await _unlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync(CurrentUser.Id);
            var unlockedTVShowIds = await _unlockedMediaService.GetUnlockedTVShowIdsForUserAsync(CurrentUser.Id);

            var filter = new MediaEntryFilter(mediaSourceId, search, genreId, page, size, includeIndividualMediaTypes);

            var movieCollections = await GetMovieCollectionEntriesAsync(filter, mediaSourceIds, unlockedMovieCollectionIds);
            var tvShows = await GetTVShowEntriesAsync(filter, mediaSourceIds, unlockedTVShowIds);

            var entries = movieCollections.Concat(tvShows);

            if (filter.IncludeIndividualMediaTypes)
            {
                var movies = await GetMovieEntriesAsync(filter, mediaSourceIds, unlockedMovieCollectionIds);
                var seasons = await GetSeasonEntriesAsync(filter, mediaSourceIds, unlockedTVShowIds);
                var episodes = await GetEpisodeEntriesAsync(filter, mediaSourceIds, unlockedTVShowIds);
                entries = entries.Concat(movies).Concat(seasons).Concat(episodes);
            }

            var pagedEntries = entries
                .OrderBy(e => e.Title)
                .Skip(page * size)
                .Take(size)
                .ToList();

            return Ok(pagedEntries);
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
    /// Bundles the media-source/search/genre/paging filter parameters shared by all five
    /// <c>Get*EntriesAsync</c> helpers below, which otherwise pass the same five values unchanged on
    /// every call alongside a type-specific id list.
    /// </summary>
    /// <param name="MediaSourceId">Optional media source id to restrict results to.</param>
    /// <param name="Search">Optional case-insensitive substring to match against the entry name.</param>
    /// <param name="GenreId">Optional genre id to restrict results to.</param>
    /// <param name="Page">Zero-based page index used for the final result trimming in <see cref="Get(long?, int, int, string?, long?, bool)"/>.</param>
    /// <param name="Size">Page size used for the final result trimming in <see cref="Get(long?, int, int, string?, long?, bool)"/>.</param>
    /// <param name="IncludeIndividualMediaTypes">
    /// When <c>true</c>, the <c>Movie</c>, <c>TVShowSeason</c> and <c>TVShowEpisode</c> helpers are included
    /// alongside <c>MovieCollection</c> and <c>TVShow</c>. Defaults to <c>false</c> so media-source browsing
    /// keeps returning only the original two media types.
    /// </param>
    private readonly record struct MediaEntryFilter(long? MediaSourceId, string? Search, long? GenreId, int Page, int Size, bool IncludeIndividualMediaTypes = false);

    /// <summary>
    /// Applies the case-insensitive substring search shared by all five <c>Get*EntriesAsync</c> helpers
    /// below to the entry's <see cref="MediaBaseEntry.Name"/>.
    /// </summary>
    /// <typeparam name="T">The entity type, which must derive from <see cref="MediaBaseEntry"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="search">Optional case-insensitive substring to match against the entry name.</param>
    /// <returns>The filtered query, or the unmodified <paramref name="query"/> when <paramref name="search"/> is empty.</returns>
    private static IQueryable<T> ApplySearchFilter<T>(IQueryable<T> query, string? search) where T : MediaBaseEntry
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;

        var lowered = search.ToLower();
        return query.Where(e => e.Name.ToLower().Contains(lowered));
    }

    private async Task<List<MediaEntryDto>> GetMovieCollectionEntriesAsync(MediaEntryFilter filter, long[] mediaSourceIds, long[] unlockedMovieCollectionIds)
    {
        var query = _db.MovieCollections
            .AsNoTracking()
            .Where(mc => !filter.MediaSourceId.HasValue || mc.MediaSourceId == filter.MediaSourceId);

        query = ApplySearchFilter(query, filter.Search);

        if (filter.GenreId.HasValue)
        {
            // Nur Collections, deren Movies das Genre haben
            query = query.Where(mc =>
                _db.Movies.Any(m =>
                    m.MovieCollectionId == mc.Id &&
                    m.MovieGenres.Any(mg => mg.GenreId == filter.GenreId.Value)
                )
            );
        }

        return await query
            .Where(m => mediaSourceIds.Contains(m.MediaSourceId) || unlockedMovieCollectionIds.Contains(m.Id))
            .OrderBy(e => e.Name)
            .Take((filter.Page + 1) * filter.Size)
            .Select(mc => new MediaEntryDto
            {
                Type = nameof(MovieCollection),
                Id = mc.Id,
                Title = mc.Name,
                Description = "",
                Url = $"/moviecollection/{mc.Id}",
                CreatedAt = mc.CreatedAt,
                PictureId = mc.PosterPictureId,
                ItemCount = _db.Movies.Count(m => m.MovieCollectionId == mc.Id)
            })
            .ToListAsync();
    }

    private async Task<List<MediaEntryDto>> GetTVShowEntriesAsync(MediaEntryFilter filter, long[] mediaSourceIds, long[] unlockedTVShowIds)
    {
        var query = _db.TVShows
            .AsNoTracking()
            .Where(ts => !filter.MediaSourceId.HasValue || ts.MediaSourceId == filter.MediaSourceId);

        query = ApplySearchFilter(query, filter.Search);

        if (filter.GenreId.HasValue)
        {
            query = query.Where(ts =>
                ts.TVShowGenres.Any(tg => tg.GenreId == filter.GenreId.Value)
            );
        }

        return await query
            .Where(m => mediaSourceIds.Contains(m.MediaSourceId) || unlockedTVShowIds.Contains(m.Id))
            .OrderBy(e => e.Name)
            .Take((filter.Page + 1) * filter.Size)
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
    }

    private async Task<List<MediaEntryDto>> GetMovieEntriesAsync(MediaEntryFilter filter, long[] mediaSourceIds, long[] unlockedMovieCollectionIds)
    {
        var query = _db.Movies
            .AsNoTracking()
            .Where(m => !filter.MediaSourceId.HasValue || m.MediaSourceId == filter.MediaSourceId);

        query = ApplySearchFilter(query, filter.Search);

        if (filter.GenreId.HasValue)
        {
            query = query.Where(m =>
                m.MovieGenres.Any(mg => mg.GenreId == filter.GenreId.Value)
            );
        }

        return await query
            .Where(m => mediaSourceIds.Contains(m.MediaSourceId) || unlockedMovieCollectionIds.Contains(m.MovieCollectionId ?? -1))
            .OrderBy(e => e.Name)
            .Take((filter.Page + 1) * filter.Size)
            .Select(m => new MediaEntryDto
            {
                Type = nameof(Movie),
                Id = m.Id,
                Title = m.Name,
                Description = m.Plot,
                Url = $"/movie/{m.Id}",
                CreatedAt = m.CreatedAt,
                PictureId = m.PosterPictureId
            })
            .ToListAsync();
    }

    private async Task<List<MediaEntryDto>> GetSeasonEntriesAsync(MediaEntryFilter filter, long[] mediaSourceIds, long[] unlockedTVShowIds)
    {
        var query = _db.TVShowSeasons
            .AsNoTracking()
            .Where(s => !filter.MediaSourceId.HasValue || s.MediaSourceId == filter.MediaSourceId);

        query = ApplySearchFilter(query, filter.Search);

        if (filter.GenreId.HasValue)
        {
            query = query.Where(s =>
                s.TVShow.TVShowGenres.Any(tg => tg.GenreId == filter.GenreId.Value)
            );
        }

        return await query
            .Where(s => mediaSourceIds.Contains(s.MediaSourceId) || unlockedTVShowIds.Contains(s.TVShowId))
            .OrderBy(e => e.Name)
            .Take((filter.Page + 1) * filter.Size)
            .Select(s => new MediaEntryDto
            {
                Type = nameof(TVShowSeason),
                Id = s.Id,
                Title = s.Name,
                Description = "",
                Url = $"/tvshow/season/{s.Id}",
                CreatedAt = s.CreatedAt,
                PictureId = s.PosterPictureId
            })
            .ToListAsync();
    }

    private async Task<List<MediaEntryDto>> GetEpisodeEntriesAsync(MediaEntryFilter filter, long[] mediaSourceIds, long[] unlockedTVShowIds)
    {
        var query = _db.TVShowEpisodes
            .AsNoTracking()
            .Where(e => !filter.MediaSourceId.HasValue || e.MediaSourceId == filter.MediaSourceId);

        query = ApplySearchFilter(query, filter.Search);

        if (filter.GenreId.HasValue)
        {
            query = query.Where(e =>
                e.TVShowSeason.TVShow.TVShowGenres.Any(tg => tg.GenreId == filter.GenreId.Value)
            );
        }

        return await query
            .Where(e => mediaSourceIds.Contains(e.MediaSourceId) || unlockedTVShowIds.Contains(e.TVShowSeason.TVShowId))
            .OrderBy(e => e.Name)
            .Take((filter.Page + 1) * filter.Size)
            .Select(e => new MediaEntryDto
            {
                Type = nameof(TVShowEpisode),
                Id = e.Id,
                Title = e.Name,
                Description = e.Plot,
                Url = $"/tvshow/episode/{e.Id}",
                CreatedAt = e.CreatedAt,
                PictureId = e.PosterPictureId
            })
            .ToListAsync();
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
        if (!_unlockedMediaService.IsAccessible(hasSourceAccess, isUnlocked))
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


