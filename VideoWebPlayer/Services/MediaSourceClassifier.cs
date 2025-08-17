using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VideoWebPlayer.Data;
using static System.Net.Mime.MediaTypeNames;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Verantwortlich für die Klassifizierung und Verarbeitung der gescannten MediaItems und MediaCollections.
    /// </summary>
    public class MediaSourceClassifier
    {
        private readonly ApplicationDbContext _db;
        private readonly SftpMediaSourceReader _sftpReader;
        private readonly RecentEntryService _recentEntryService;
        private readonly ILogger<MediaSourceClassifier> _logger;
        private readonly string[] fileExtensions_Video = new string[] { ".mp4", ".avi", ".mkv", ".mpeg" };
        private static readonly string[] PictureTypes = new[] { "poster", "banner", "fanart", "thumb" };
        private static readonly string[] MovieCollectionPictureNames = new[] { "folder", "banner", "poster", "fanart" };
        private static readonly string[] ImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] TVShowPictureTypes = new[] { "poster", "banner", "fanart", "thumb", "" };
        private static readonly string[] TVShowImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        public MediaSourceClassifier(
            ApplicationDbContext db,
            SftpMediaSourceReader sftpReader,
            RecentEntryService recentEntryService,
            ILogger<MediaSourceClassifier> logger)
        {
            _db = db;
            _sftpReader = sftpReader;
            _recentEntryService = recentEntryService;
            _logger = logger;
        }

        /// <summary>
        /// Führt die Klassifizierung aller relevanten MediaItems und MediaCollections durch.
        /// </summary>
        public async Task ClassifyAllAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starte Klassifizierung aller MediaItems und MediaCollections.");
            await ProcessMediaItemsAsync(cancellationToken);
            await ProcessMediaCollectionsAsync(cancellationToken);
            _logger.LogInformation("Klassifizierung abgeschlossen.");
        }

        private async Task ProcessMediaItemsAsync(CancellationToken cancellationToken)
        {
            var items = _db.MediaItems
                .Include(mi => mi.MediaCollection)
                .Where(mi => mi.Changed || !mi.ClassifiedAt.HasValue)
                .ToList();

            _logger.LogInformation("Klassifiziere MediaItems.");
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                if (item.MediaCollection != null)
                    item.MediaCollection.Changed = true;
                item.Changed = false;
                item.ClassifiedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task ProcessMediaCollectionsAsync(CancellationToken cancellationToken)
        {
            var collections = _db.MediaCollections
                .Include(mc => mc.MediaSource)
                .Where(mc => mc.Changed || !mc.ClassifiedAt.HasValue)
                .ToList();

            _logger.LogInformation("Klassifiziere MediaCollections.");
            foreach (var collection in collections)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                _logger.LogInformation("Verarbeite Collection '{CollectionName}' (ID: {CollectionId})", collection.Name, collection.Id);

                // 1. TVShow-Verarbeitung
                await ProcessCollectionAsTVShowAsync(collection, cancellationToken);

                // 2. Movie-Verarbeitung (falls TVShow nicht zutrifft oder zusätzlich nötig)
                await ProcessCollectionAsMovieAsync(collection, cancellationToken);

                collection.Changed = false;
                collection.ClassifiedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Prüft und verarbeitet eine Collection als TVShow (z.B. wenn tvshow.nfo existiert).
        /// </summary>
        private async Task ProcessCollectionAsTVShowAsync(MediaCollection collection, CancellationToken cancellationToken)
        {
            bool hasTvShowNfo = await _sftpReader.FileExistsAsync(collection, "tvshow.nfo");
            if (!hasTvShowNfo)
            {
                _logger.LogDebug("Collection '{CollectionName}' hat keine tvshow.nfo.", collection.Name);
                return;
            }

            var nfoContent = await _sftpReader.ReadFileAsync(collection, "tvshow.nfo");
            if (string.IsNullOrWhiteSpace(nfoContent))
            {
                _logger.LogWarning("tvshow.nfo in Collection '{CollectionName}' ist leer.", collection.Name);
                return;
            }

            XElement? xml = null;
            try
            {
                xml = XElement.Parse(nfoContent);
            }
            catch
            {
                _logger.LogWarning("tvshow.nfo in Collection '{CollectionName}' ist kein gültiges XML.", collection.Name);
                return;
            }

            _logger.LogInformation("Verarbeite TVShow für Collection '{CollectionName}'.", collection.Name);
            var show = await CreateOrUpdateTVShow(collection, xml, cancellationToken);
            await ProcessEpisodesForTVShowAsync(show, cancellationToken);
        }

        private async Task<TVShow> CreateOrUpdateTVShow(MediaCollection collection, XElement xml, CancellationToken cancellationToken)
        {
            // Parse die relevanten Infos aus dem XML
            string showName = xml.Element("title")?.Value ?? collection.Name;

            // Prüfe, ob es bereits einen TVShow-Datensatz zu dieser Collection gibt
            var existingShow = await _db.TVShows
                .FirstOrDefaultAsync(s => s.MediaSourceId == collection.MediaSourceId && s.Name == showName, cancellationToken);

            if (existingShow == null)
            {
                var tvShow = new TVShow
                {
                    Name = showName,
                    MediaSourceId = collection.MediaSourceId,
                    CollectionId = collection.Id,
                    CreatedAt = DateTime.UtcNow
                };
                tvShow.LoadFromXml(xml);
                _db.TVShows.Add(tvShow);
                existingShow = tvShow;
                await _db.SaveChangesAsync(cancellationToken);
                await _recentEntryService.AddTVShowAsync(tvShow).ConfigureAwait(false);
                _logger.LogInformation("Neue TVShow '{ShowName}' angelegt.", showName);
            }
            else
            {
                existingShow.Name = showName;
                existingShow.LoadFromXml(xml);
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("TVShow '{ShowName}' aktualisiert.", showName);
            }
            var showGenres = await GetOrCreateGenresAsync(existingShow.GenreNames, collection.MediaSourceId, cancellationToken);
            existingShow.Genres.Clear();
            foreach (var genre in showGenres)
                existingShow.Genres.Add(genre);
            existingShow.GenreNames = string.Join(",", existingShow.Genres.Select(g => g.Name));
            await _db.SaveChangesAsync(cancellationToken);
            return existingShow;
        }

        // Dummy-Parser für ShowName aus NFO (bitte durch echtes XML-Parsing ersetzen)
        private string? ParseShowNameFromNfo(string nfoContent)
        {
            try
            {
                using var reader = new System.IO.StringReader(nfoContent);
                var xml = System.Xml.Linq.XDocument.Load(reader);
                var title = xml.Root?.Element("title")?.Value;
                return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
            }
            catch
            {
                return null;
            }
        }

        private async Task ProcessEpisodesForTVShowAsync(
            TVShow show,
            CancellationToken cancellationToken)
        {
            // Alle MediaItems aus der Collection und allen Unter-Collections laden
            var collectionIds = await _db.MediaCollections
                .Where(c => c.Id == show.CollectionId || c.ParentMediaCollectionId == show.CollectionId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var mediaItems = await _db.MediaItems
                .Where(mi => collectionIds.Contains(mi.MediaCollectionId))
                .Include(mi => mi.MediaCollection)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Verarbeite {Count} Episoden für TVShow '{ShowName}'.", mediaItems.Count, show.Name);

            var isFirst = true;

            foreach (var item in mediaItems)
            {
                var ext = Path.GetExtension(item.Path);
                if (!fileExtensions_Video.Contains(ext))
                    continue;

                // NFO-Dateiname bestimmen
                var nfoFileName = System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(item.Path), ".nfo");
                var collection = item.MediaCollection;

                // Prüfen, ob NFO existiert
                bool nfoExists = await _sftpReader.FileExistsAsync(collection, nfoFileName);
                if (!nfoExists)
                    continue;

                // NFO laden
                var nfoContent = await _sftpReader.ReadFileAsync(collection, nfoFileName);
                if (string.IsNullOrWhiteSpace(nfoContent))
                    continue;

                // XML parsen
                XElement? xml = null;
                try { xml = XElement.Parse(nfoContent); }
                catch { continue; }

                // Staffelnummer und Episodennummer auslesen
                int seasonNo = int.TryParse(xml.Element("season")?.Value, out var s) ? s : 0;
                var seasonName = seasonNo == 0 ? "Specials" : $"Staffel {seasonNo.ToString().PadLeft(2, '0')}";
                int episodeNo = int.TryParse(xml.Element("episode")?.Value, out var e) ? e : 0;
                if (episodeNo == 0)
                    continue; // Keine Episode-Nummer, überspringen

                // Staffel suchen oder anlegen
                var season = await _db.TVShowSeasons
                    .FirstOrDefaultAsync(se =>
                        se.TVShowId == show.Id && se.Name == $"{seasonName}", cancellationToken);
                if (season == null)
                {
                    season = new TVShowSeason
                    {
                        TVShowId = show.Id,
                        Name = seasonName,
                        CreatedAt = DateTime.UtcNow,
                        MediaSourceId = show.MediaSourceId,
                        CollectionId = collection.Id,
                    };
                    _db.TVShowSeasons.Add(season);
                    await _db.SaveChangesAsync(cancellationToken);
                    await _recentEntryService.AddTVShowSeasonAsync(season).ConfigureAwait(false);
                    _logger.LogInformation("Neue Staffel '{SeasonName}' für TVShow '{ShowName}' angelegt.", seasonName, show.Name);
                }

                // Episode suchen oder anlegen
                var episodeTitle = xml.Element("title")?.Value ?? item.Name;
                var existingEpisode = await _db.TVShowEpisodes
                    .FirstOrDefaultAsync(ep =>
                        ep.TVShowSeasonId == season.Id &&
                        ep.Number == episodeNo, cancellationToken);

                if (existingEpisode == null)
                {
                    var episode = new TVShowEpisode
                    {
                        Name = episodeTitle,
                        Number = episodeNo,
                        TVShowSeasonId = season.Id,
                        MediaSourceId = show.MediaSourceId,
                        CreatedAt = DateTime.UtcNow,
                        ReleaseDate = DateTime.TryParse(xml.Element("aired")?.Value, out var aired) ? aired : (DateTime?)null,
                        PremieredAt = DateTime.TryParse(xml.Element("premiered")?.Value, out var prem) ? prem : (DateTime?)null,
                        Plot = xml.Element("plot")?.Value,
                        // Weitere Felder aus xml setzen
                    };
                    _db.TVShowEpisodes.Add(episode);
                    existingEpisode = episode;
                    await _db.SaveChangesAsync(cancellationToken);
                    await _recentEntryService.AddTVShowEpisodeAsync(episode).ConfigureAwait(false);
                    _logger.LogInformation("Neue Episode '{EpisodeTitle}' (Staffel {SeasonNo}, Episode {EpisodeNo}) angelegt.", episodeTitle, seasonNo, episodeNo);
                }
                else
                {
                    existingEpisode.Name = episodeTitle;
                    existingEpisode.ReleaseDate = DateTime.TryParse(xml.Element("aired")?.Value, out var aired) ? aired : (DateTime?)null;
                    existingEpisode.PremieredAt = DateTime.TryParse(xml.Element("premiered")?.Value, out var prem) ? prem : (DateTime?)null;
                    existingEpisode.Plot = xml.Element("plot")?.Value;
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Episode '{EpisodeTitle}' (Staffel {SeasonNo}, Episode {EpisodeNo}) aktualisiert.", episodeTitle, seasonNo, episodeNo);
                }                

                var episodeMediaItem = await _db.TVShowEpisodeMediaItems.FirstOrDefaultAsync(i => i.TVShowEpisodeId == existingEpisode.Id && i.MediaItemId == item.Id);
                if (episodeMediaItem is null)
                {
                    episodeMediaItem = new TVShowEpisodeMediaItem
                    {
                        TVShowEpisodeId = existingEpisode.Id,
                        MediaItemId = item.Id
                    };
                    _db.TVShowEpisodeMediaItems.Add(episodeMediaItem);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                if (existingEpisode.ReleaseDate.HasValue)
                {
                    season.ReleaseDate =
                        season.ReleaseDate.HasValue
                            ? (season.ReleaseDate < existingEpisode.ReleaseDate ? season.ReleaseDate : existingEpisode.ReleaseDate)
                            : existingEpisode.ReleaseDate;
                    show.ReleaseDate =
                        show.ReleaseDate.HasValue
                            ? (show.ReleaseDate < existingEpisode.ReleaseDate ? show.ReleaseDate : existingEpisode.ReleaseDate)
                            : existingEpisode.ReleaseDate;
                }
                if (existingEpisode.PremieredAt.HasValue)
                {
                    season.PremieredAt =
                        season.PremieredAt.HasValue
                            ? (season.PremieredAt < existingEpisode.PremieredAt ? season.PremieredAt : existingEpisode.PremieredAt)
                            : existingEpisode.PremieredAt;
                    show.PremieredAt =
                        show.PremieredAt.HasValue
                            ? (show.PremieredAt < existingEpisode.PremieredAt ? show.PremieredAt : existingEpisode.PremieredAt)
                            : existingEpisode.PremieredAt;
                }
                await _db.SaveChangesAsync(cancellationToken);
                await AssignPicturesToTVShowEpisodeAsync(existingEpisode, collection, item.Path, cancellationToken);
                await AssignPicturesToTVShowSeasonAsync(show, season, collection, cancellationToken, isFirst);
                await AssignPicturesToTVShowAsync(show, collection, cancellationToken, isFirst);
                isFirst = false;
            }
        }


        /// <summary>
        /// Prüft und verarbeitet eine Collection als Movie-Collection.
        /// </summary>
        private async Task ProcessCollectionAsMovieAsync(MediaCollection collection, CancellationToken cancellationToken)
        {
            // 1. Alle MediaItems (Videodateien) der Collection laden
            var mediaItems = await _db.MediaItems
                .Where(mi => mi.MediaCollectionId == collection.Id)
                .ToListAsync(cancellationToken);

            var movies = new List<Movie>();
            foreach (var item in mediaItems)
            {
                var ext = Path.GetExtension(item.Path);
                if (!fileExtensions_Video.Contains(ext))
                    continue;
                // NFO-Dateiname bestimmen
                var nfoFileName = System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(item.Path), ".nfo");

                // Prüfen, ob NFO existiert
                bool nfoExists = await _sftpReader.FileExistsAsync(collection, nfoFileName);
                if (!nfoExists)
                    continue;

                // NFO laden
                var nfoContent = await _sftpReader.ReadFileAsync(collection, nfoFileName);
                if (string.IsNullOrWhiteSpace(nfoContent))
                    continue;

                // XML parsen
                XElement? xml = null;
                try { xml = XElement.Parse(nfoContent); }
                catch { continue; }

                // Prüfen, ob es sich um einen Movie handelt
                if (!string.Equals(xml.Name.LocalName, "movie", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Movie-Name bestimmen
                var movieName = xml.Element("title")?.Value ?? item.Name;

                // Movie suchen oder anlegen
                var existingMovie = await _db.Movies
                    .FirstOrDefaultAsync(m => m.CollectionId == collection.Id && m.Name == movieName, cancellationToken);

                if (existingMovie == null)
                {
                    var movie = new Movie
                    {
                        Name = movieName,
                        MediaSourceId = collection.MediaSourceId,
                        CollectionId = collection.Id,
                        CreatedAt = DateTime.UtcNow,
                        // ggf. weitere Initialwerte
                    };
                    movie.LoadFromXml(xml); // <-- XML-Daten setzen
                    _db.Movies.Add(movie);
                    await _db.SaveChangesAsync(cancellationToken);
                    existingMovie = movie;
                    movies.Add(movie);
                    await _recentEntryService.AddMovieAsync(movie).ConfigureAwait(false);
                    _logger.LogInformation("Neuer Film '{MovieName}' angelegt.", movieName);
                }
                else
                {
                    existingMovie.Name = movieName;
                    existingMovie.LoadFromXml(xml); // <-- XML-Daten aktualisieren
                    await _db.SaveChangesAsync(cancellationToken);
                    movies.Add(existingMovie);
                    _logger.LogInformation("Film '{MovieName}' aktualisiert.", movieName);
                }
                var movieGenres = await GetOrCreateGenresAsync(existingMovie.GenreNames, collection.MediaSourceId, cancellationToken);
                existingMovie.Genres.Clear();
                foreach (var genre in movieGenres)
                    existingMovie.Genres.Add(genre);
                existingMovie.GenreNames = string.Join(",", existingMovie.Genres.Select(g => g.Name));
                await _db.SaveChangesAsync(cancellationToken);

                var movieMediaItem = await _db.MovieMediaItems
                    .FirstOrDefaultAsync(mmi => mmi.MovieId == existingMovie.Id && mmi.MediaItemId == item.Id, cancellationToken);
                if (movieMediaItem is null)
                {
                    movieMediaItem = new MovieMediaItem
                    {
                        MovieId = existingMovie.Id,
                        MediaItemId = item.Id
                    };
                    _db.MovieMediaItems.Add(movieMediaItem);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await AssignPicturesToMovieAsync(existingMovie, collection, cancellationToken);
            }

            if (movies.Count > 0)
            {
                // 2. MovieCollection-Name bestimmen
                var collectionName = GetCommonPrefix(movies.Select(m => m.Name).ToList());
                if (string.IsNullOrWhiteSpace(collectionName) || collectionName.Length < 3)
                    collectionName = collection.Name;

                // 3. MovieCollection suchen oder anlegen
                var existingCollection = await _db.MovieCollections
                    .FirstOrDefaultAsync(mc => mc.MediaSourceId == collection.MediaSourceId && mc.CollectionId == collection.Id, cancellationToken);

                if (existingCollection == null)
                {
                    var movieCollection = new MovieCollection
                    {
                        Name = collectionName,
                        MediaSourceId = collection.MediaSourceId,
                        CollectionId = collection.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.MovieCollections.Add(movieCollection);
                    await _db.SaveChangesAsync(cancellationToken);
                    existingCollection = movieCollection;
                    // Movies zuordnen
                    foreach (var movie in movies)
                    {
                        movie.MovieCollection = movieCollection;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                    await _recentEntryService.AddMovieCollectionAsync(movieCollection).ConfigureAwait(false);
                    _logger.LogInformation("Neue MovieCollection '{CollectionName}' angelegt.", collectionName);
                }
                else
                {
                    existingCollection.Name = collectionName;
                    await _db.SaveChangesAsync(cancellationToken);
                    foreach (var movie in movies)
                    {
                        movie.MovieCollection = existingCollection;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                    _logger.LogInformation("MovieCollection '{CollectionName}' aktualisiert.", collectionName);
                }
                await AssignPicturesToMovieCollectionAsync(existingCollection, collection, cancellationToken);
            }
        }

        private async Task AssignPicturesToMovieAsync(Movie movie, MediaCollection collection, CancellationToken cancellationToken)
        {
            // Alle zugeordneten MediaItems des Movies laden
            var movieMediaItems = await _db.MovieMediaItems
                .Include(mmi => mmi.MediaItem)
                .Where(mmi => mmi.MovieId == movie.Id)
                .ToListAsync(cancellationToken);

            if (movieMediaItems.Count == 0)
                return;

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var images = await _db.MediaItems
                .Where(mi => mi.MediaCollectionId == collection.Id)
                .ToListAsync(cancellationToken);
            images = images.Where(mi => imageExtensions.Contains(Path.GetExtension(mi.Path).ToLower()))
                .ToList();

            foreach (var mmi in movieMediaItems)
            {
                var movieBaseName = Path.GetFileNameWithoutExtension(mmi.MediaItem.Path).ToLower();

                foreach (var img in images)
                {
                    var fileName = Path.GetFileNameWithoutExtension(img.Path).ToLower();
                    foreach (var type in PictureTypes)
                    {
                        if (fileName.StartsWith(movieBaseName + "-" + type))
                        {
                            Picture? picture = await GetOrCreatePicture(img, type, cancellationToken);
                            if (type == "poster")
                                movie.PosterPictureId = picture.Id;
                            else if (type == "banner")
                                movie.BannerPictureId = picture.Id;
                            else if (type == "fanart")
                                movie.FanartPictureId = picture.Id;
                            await _db.SaveChangesAsync(cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task<Picture?> GetOrCreatePicture(MediaItem img, string type, CancellationToken cancellationToken)
        {
            var ext = Path.GetExtension(img.Path);
            var picture = await _db.Pictures.FirstOrDefaultAsync(p => p.MediaItemId == img.Id && p.Type == type, cancellationToken);
            if (picture == null)
            {
                picture = new Picture
                {
                    MediaItemId = img.Id,
                    Type = type,
                    Description = img.Name,
                    ContentType = $"image/{ext}",
                    Data = new byte[0]
                };
                _db.Pictures.Add(picture);
                await _db.SaveChangesAsync(cancellationToken);
            }
            if (picture.Data is null || picture.Data.Length == 0)
            {
                var imageBytes = await _sftpReader.ReadFileStreamAsync(img.MediaCollection, Path.GetFileName(img.Path));
                picture.Data = ConvertStreamToByteArray(imageBytes);
                picture.ContentType = $"image/{ext}";
                await _db.SaveChangesAsync(cancellationToken);
            }
            if (string.IsNullOrWhiteSpace(picture.ContentType))
            {
                picture.ContentType = $"image/{ext}";
                await _db.SaveChangesAsync(cancellationToken);
            }
            return picture;
        }

        private async Task AssignPicturesToMovieCollectionAsync(MovieCollection movieCollection, MediaCollection collection, CancellationToken cancellationToken)
        {
            foreach (var baseName in MovieCollectionPictureNames)
            {
                foreach (var ext in ImageExtensions)
                {
                    var fileName = $"{baseName}{ext}";
                    bool exists = await _sftpReader.FileExistsAsync(collection, fileName);
                    if (exists)
                    {
                        // Prüfe, ob das Bild bereits als MediaItem existiert
                        var mediaItem = (await _db.MediaItems
                            .Where(mi => mi.MediaCollectionId == collection.Id && mi.Path.EndsWith(fileName))
                            .ToListAsync(cancellationToken)
                            ).FirstOrDefault(mi => Path.GetFileName(mi.Path) == fileName);

                        if (mediaItem == null)
                        {
                            // Optional: MediaItem anlegen, falls gewünscht
                            continue;
                        }

                        Picture? picture = await GetOrCreatePicture(mediaItem, baseName, cancellationToken);
                        // Weisen das Bild der MovieCollection zu
                        switch (baseName)
                        {
                            case "poster":
                                movieCollection.PosterPictureId = picture.Id;
                                break;
                            case "banner":
                                movieCollection.BannerPictureId = picture.Id;
                                break;
                            case "fanart":
                                movieCollection.FanartPictureId = picture.Id;
                                break;
                            case "folder":
                                if (movieCollection.PosterPictureId is null)
                                    movieCollection.PosterPictureId = picture.Id;
                                break;
                        }
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            // Falls kein explizites Poster oder Banner gefunden wurde, nimm das erste aus den Filmen
            if (movieCollection.PosterPictureId == null || movieCollection.BannerPictureId == null || movieCollection.FanartPictureId == null)
            {
                var firstMovieWithPoster = await _db.Movies
                    .Where(m => m.MovieCollectionId == movieCollection.Id && m.PosterPictureId != null)
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (movieCollection.PosterPictureId == null && firstMovieWithPoster?.PosterPictureId != null)
                    movieCollection.PosterPictureId = firstMovieWithPoster.PosterPictureId;
                await _db.SaveChangesAsync(cancellationToken);

                var firstMovieWithBanner = await _db.Movies
                    .Where(m => m.MovieCollectionId == movieCollection.Id && m.BannerPictureId != null)
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (movieCollection.BannerPictureId == null && firstMovieWithBanner?.BannerPictureId != null)
                    movieCollection.BannerPictureId = firstMovieWithBanner.BannerPictureId;
                await _db.SaveChangesAsync(cancellationToken);

                var firstMovieWithFanart= await _db.Movies
                    .Where(m => m.MovieCollectionId == movieCollection.Id && m.FanartPictureId != null)
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (movieCollection.FanartPictureId == null && firstMovieWithFanart?.FanartPictureId != null)
                    movieCollection.FanartPictureId = firstMovieWithFanart.FanartPictureId;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task AssignPicturesToTVShowAsync(TVShow show, MediaCollection episodeCollection, CancellationToken cancellationToken, bool isFirst)
        {
            if (!isFirst && (show.PosterPictureId ?? 0) != 0 && (show.BannerPictureId ?? 0) != 0)
                return;
            // 1. TVShow-Bilder direkt in der Collection suchen
            var pictureIds = new Dictionary<string, long?>();
            var collection = episodeCollection;
            while (collection is not null)
            {
                if (collection.Id == show.CollectionId)
                    foreach (var type in TVShowPictureTypes)
                    {
                        foreach (var ext in TVShowImageExtensions)
                        {
                            var fileName = $"{type}{ext}";
                            bool exists = await _sftpReader.FileExistsAsync(collection, fileName);
                            if (exists)
                            {
                                var mediaItem = await _db.MediaItems
                                    .FirstOrDefaultAsync(mi => mi.MediaCollectionId == collection.Id && mi.Path.EndsWith(fileName), cancellationToken);

                                if (mediaItem == null)
                                    continue;

                                Picture? picture = await GetOrCreatePicture(mediaItem, type, cancellationToken);                                

                                pictureIds[type] = picture.Id;
                            }
                        }
                    }
                collection = collection.ParentMediaCollection;
            }

            // 2. Fallback: Von Staffel übernehmen, falls kein Bild gefunden
            if (!pictureIds.ContainsKey("poster") || !pictureIds.ContainsKey("banner") || !pictureIds.ContainsKey("fanart"))
            {
                var firstSeasonWithBanner = await _db.TVShowSeasons
                    .Where(s => s.TVShowId == show.Id && s.BannerPictureId != null)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstSeasonWithBanner != null)
                {
                    if (!pictureIds.ContainsKey("banner") && firstSeasonWithBanner.BannerPictureId != null)
                        pictureIds["banner"] = firstSeasonWithBanner.BannerPictureId;
                }

                var firstSeasonWithPoster = await _db.TVShowSeasons
                    .Where(s => s.TVShowId == show.Id && s.PosterPictureId != null)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstSeasonWithPoster != null)
                {
                    if (!pictureIds.ContainsKey("poster") && firstSeasonWithPoster.PosterPictureId != null)
                        pictureIds["poster"] = firstSeasonWithPoster.PosterPictureId;                    
                }

                var firstSeasonWithFanart = await _db.TVShowSeasons
                    .Where(s => s.TVShowId == show.Id && s.FanartPictureId != null)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstSeasonWithFanart != null)
                {
                    if (!pictureIds.ContainsKey("fanart") && firstSeasonWithFanart.FanartPictureId != null)
                        pictureIds["fanart"] = firstSeasonWithFanart.FanartPictureId;
                }
            }

            show.PosterPictureId = pictureIds.ContainsKey("poster") ? pictureIds["poster"] : null;
            show.BannerPictureId = pictureIds.ContainsKey("banner") ? pictureIds["banner"] : null;
            show.FanartPictureId = pictureIds.ContainsKey("fanart") ? pictureIds["fanart"] : null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task AssignPicturesToTVShowSeasonAsync(TVShow show, TVShowSeason season, MediaCollection tvShowCollection, CancellationToken cancellationToken, bool isFirst)
        {
            if (!isFirst && (season.PosterPictureId ?? 0) != 0 && (season.BannerPictureId ?? 0) != 0)
                return;

            // 1. Staffelbilder in der TVShow-Collection suchen (z.B. season02-banner.jpg)
            var seasonNumber = season.Name?.ToLower().Replace("staffel", "").Trim().PadLeft(2, '0') ?? "01";
            var pictureIds = new Dictionary<string, long?>();
            var collection = tvShowCollection;
            while (collection is not null)
            {
                if (collection.Id == show.CollectionId || collection.Id == season.CollectionId)
                    foreach (var type in TVShowPictureTypes)
                    {
                        foreach (var ext in TVShowImageExtensions)
                        {
                            var fileName = $"season{seasonNumber}-{type}{ext}";
                            bool exists = await _sftpReader.FileExistsAsync(collection, fileName);
                            if (exists)
                            {
                                var mediaItem = await _db.MediaItems
                                    .FirstOrDefaultAsync(mi => mi.MediaCollectionId == collection.Id && mi.Path.EndsWith(fileName), cancellationToken);

                                if (mediaItem == null)
                                    continue;

                                Picture? picture = await GetOrCreatePicture(mediaItem, type, cancellationToken);                                

                                pictureIds[type] = picture.Id;
                            }
                        }
                    }
                collection = collection.ParentMediaCollection;
            }

            // 2. Fallback: Von Episode übernehmen, falls kein Bild gefunden
            if (!pictureIds.ContainsKey("poster") || !pictureIds.ContainsKey("banner") || !pictureIds.ContainsKey("fanart"))
            {
                var firstEpisodeWithBanner = await _db.TVShowEpisodes
                    .Where(e => e.TVShowSeasonId == season.Id && e.BannerPictureId != null)
                    .OrderBy(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstEpisodeWithBanner != null)
                {
                    if (!pictureIds.ContainsKey("banner") && firstEpisodeWithBanner.BannerPictureId != null)
                        pictureIds["banner"] = firstEpisodeWithBanner.BannerPictureId;
                }

                var firstEpisodeWithPoster = await _db.TVShowEpisodes
                    .Where(e => e.TVShowSeasonId == season.Id && e.PosterPictureId != null)
                    .OrderBy(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstEpisodeWithPoster != null)
                {
                    if (!pictureIds.ContainsKey("poster") && firstEpisodeWithPoster.PosterPictureId != null)
                        pictureIds["poster"] = firstEpisodeWithPoster.PosterPictureId;                    
                }

                var firstEpisodeWithFanart = await _db.TVShowEpisodes
                    .Where(e => e.TVShowSeasonId == season.Id && e.FanartPictureId != null)
                    .OrderBy(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstEpisodeWithFanart != null)
                {
                    if (!pictureIds.ContainsKey("fanart") && firstEpisodeWithFanart.FanartPictureId != null)
                        pictureIds["fanart"] = firstEpisodeWithFanart.FanartPictureId;
                }
            }

            season.PosterPictureId = pictureIds.ContainsKey("poster") ? pictureIds["poster"] : null;
            season.BannerPictureId = pictureIds.ContainsKey("banner") ? pictureIds["banner"] : null;
            season.FanartPictureId = pictureIds.ContainsKey("fanart") ? pictureIds["fanart"] : null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task AssignPicturesToTVShowEpisodeAsync(TVShowEpisode episode, MediaCollection collection, string videoFileName, CancellationToken cancellationToken)
        {
            // 1. Episodenbild ergibt sich aus dem Dateinamen der Videodatei (z.B. Folge01-thumb.jpg)
            foreach (var type in TVShowPictureTypes)
            {
                var postfix = $"-{type}";
                if (postfix == "-") postfix = "";
                foreach (var ext in TVShowImageExtensions)
                {
                    var baseName = System.IO.Path.GetFileNameWithoutExtension(videoFileName);
                    var fileName = $"{baseName}{postfix}{ext}";
                    bool exists = await _sftpReader.FileExistsAsync(collection, fileName);
                    if (exists)
                    {
                        var mediaItem = await _db.MediaItems
                            .FirstOrDefaultAsync(mi => mi.MediaCollectionId == collection.Id && mi.Path.EndsWith(fileName), cancellationToken);

                        if (mediaItem == null)
                            continue;

                        Picture? picture = await GetOrCreatePicture(mediaItem, type, cancellationToken);
                        
                        if (type == "poster")
                            episode.PosterPictureId = picture.Id;
                        if (type == "banner")
                            episode.BannerPictureId = picture.Id;
                        if (type == "fanart")
                            episode.FanartPictureId = picture.Id;
                        if (type == "thumb")
                            if (episode.PosterPictureId is null)
                                episode.PosterPictureId = picture.Id;
                        if (type == "")
                            if (episode.PosterPictureId is null)
                                episode.PosterPictureId = picture.Id;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }
        /// <summary>
        /// Gibt das längste gemeinsame Präfix aller Strings in der Liste zurück.
        /// </summary>
        private static string GetCommonPrefix(List<string> strings)
        {
            if (strings == null || strings.Count == 0)
                return string.Empty;

            string prefix = strings[0];
            for (int i = 1; i < strings.Count; i++)
            {
                int j = 0;
                while (j < prefix.Length && j < strings[i].Length && prefix[j] == strings[i][j])
                {
                    j++;
                }
                prefix = prefix.Substring(0, j);
                if (string.IsNullOrEmpty(prefix))
                    break;
            }
            return prefix.Trim();
        }

        private static byte[] ConvertStreamToByteArray(Stream stream)
        {
            if (stream is MemoryStream ms)
                return ms.ToArray();
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public async Task ReloadGenres(CancellationToken cancellationToken)
        {
            // Filme ohne GenreNames korrigieren
            var movies = await _db.Movies
                .Where(m => !string.IsNullOrWhiteSpace(m.GenreNames))
                .ToListAsync(cancellationToken);

            foreach (var movie in movies)
            {
                // Genre-Datensätze anlegen und zuordnen
                var genres = await GetOrCreateGenresAsync(movie.GenreNames, movie.MediaSourceId, cancellationToken);
                movie.Genres.Clear();
                foreach (var genre in genres)
                    movie.Genres.Add(genre);
                movie.GenreNames = string.Join(",", movie.Genres.Select(g => g.Name));
                await _db.SaveChangesAsync(cancellationToken);
            }

            // TVShows ohne GenreNames korrigieren
            var tvshows = await _db.TVShows
                .Where(s => !string.IsNullOrWhiteSpace(s.GenreNames))
                .ToListAsync(cancellationToken);

            foreach (var show in tvshows)
            {
                // Genre-Datensätze anlegen und zuordnen
                var genres = await GetOrCreateGenresAsync(show.GenreNames, show.MediaSourceId, cancellationToken);
                show.Genres.Clear();
                foreach (var genre in genres)
                    show.Genres.Add(genre);
                show.GenreNames = string.Join(",", show.Genres.Select(g => g.Name));
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        // Hilfsmethode wie oben beschrieben
        private async Task<List<Genre>> GetOrCreateGenresAsync(string? genreString, long mediaSourceId, CancellationToken cancellationToken)
        {
            var resultGenres = new List<Genre>();
            if (string.IsNullOrWhiteSpace(genreString))
                return resultGenres;

            var genreNames = genreString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(g => g.Trim())
                                        .Where(g => !string.IsNullOrWhiteSpace(g))
                                        .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var name in genreNames)
            {
                // 1. Suche Genre mit gleichem Namen in dieser Quelle
                var genre = (await _db.Genres
                    .Include(g => g.AlternateNames)
                    .Where(g => g.MediaSourceId == mediaSourceId)
                    .ToListAsync(cancellationToken))
                    .FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                // 2. Falls nicht gefunden, suche nach Alternativnamen
                if (genre == null)
                {
                    var newGenre = new Genre { Name = name, MediaSourceId = mediaSourceId };
                    _db.Genres.Add(newGenre);
                    await _db.SaveChangesAsync(cancellationToken);
                    resultGenres.Add(newGenre);
                }
                else
                {
                    var alternateNames = string.Join(", ", await _db.GenreNames.Where(a => a.GenreId == genre.Id && a.Name != genre.Name).Select(a => a.Name).ToListAsync());
                    if (string.IsNullOrWhiteSpace(alternateNames))
                    {
                        resultGenres.Add(genre);
                        continue;
                    }
                    resultGenres.AddRange(await GetOrCreateGenresAsync(alternateNames, mediaSourceId, cancellationToken));
                    continue;
                }
            }

            // Duplikate entfernen (z.B. falls mehrere Alternativen auf dasselbe Genre zeigen)
            return resultGenres
                .GroupBy(g => g.Id)
                .Select(g => g.First())
                .ToList();
        }

        internal async Task CheckReloadGenres(CancellationToken stoppingToken)
        {
            var setup = await _db.Setups.FirstOrDefaultAsync();
            if (setup is null || !setup.GenresChanged)
                return;
            setup.GenresChanged = false;
            await _db.SaveChangesAsync();
            try
            {
                await (ReloadGenres(stoppingToken));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Reload der Genres.");
                setup = await _db.Setups.FirstOrDefaultAsync();
                setup.GenresChanged = true;
                await _db.SaveChangesAsync();
            }            
        }
    }
}