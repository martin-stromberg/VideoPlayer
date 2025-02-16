using Microsoft.Extensions.Logging;
using Renci.SshNet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using VideoPlayer.Extensions;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.MediaInformation;
using VideoPlayer.Service.Library.Models.Sources;
using VideoPlayer.Service.Library.SourceReader;
using VideoPlayer.Tools;
using Image = SixLabors.ImageSharp.Image;

namespace VideoPlayer.Service.Library.Scanner.Classification
{
    public class VideoClassifier: BaseClassifier
    {

        private static string[] trailerExtensions = new string[]
        {
            "-trailer.avi",
            "-trailer.mp4",
            "-trailer.mov",
            "-trailer.mkv"
        };
        private static string[] videoExtensions = new string[] { ".avi", ".mp4", ".mov", ".mkv" };
        private static string[] nfoExtensions = new string[] { ".nfo" };
        private static string[] pictureExtensions = new string[] { ".jpg", ".gif", ".png" };
        private static string infoFileTVShow = "tvshow.nfo";

        public VideoClassifier(IMediaLibrary mediaLibrary, ILogger logger)
            : base(mediaLibrary, logger) { }

        public override bool Classify(MediaItem mediaItem)
        {            
            var ext = Path.GetExtension(mediaItem.Name).ToLower();
            var trailerExt = trailerExtensions.FirstOrDefault(ex => mediaItem.Name.ToLower().EndsWith(ex));
            if (!string.IsNullOrWhiteSpace(trailerExt))
                return ClassifyTrailer(mediaItem, trailerExt);
            else if (videoExtensions.Contains(ext))
                return ClassifyVideo(mediaItem);
            else if (nfoExtensions.Contains(ext))
                return ClassifyInfoFile(mediaItem);
            else if (pictureExtensions.Contains(ext))
                return ClassifyPictureFile(mediaItem);
            return false;
        }

        private bool ClassifyTrailer(MediaItem mediaItem, string trailerExt)
        {
            foreach (var ext in videoExtensions)
            {
                var originalPath = mediaItem.Path.Replace(trailerExt, ext);
                var originalMediaItem = MediaLibrary.GetMediaItemByPath(mediaItem.ParentCollectionId, originalPath);
                if (originalMediaItem is not null)
                    try
                    {
                        ClassifyVideo(originalMediaItem);
                    }
                    finally
                    {
                        MediaLibrary.Release(originalMediaItem);
                    }
            }
            return true;
        }

        private bool ClassifyVideo(MediaItem mediaItem)
        {         
            var collection = MediaLibrary.GetMediaCollection(mediaItem.ParentCollectionId);
            if (collection is not null)
                try
                {
                    var source = MediaLibrary.GetSource(collection.SourceId);
                    try
                    {
                        var reader = CreateReader(source);
                        var parentMetaInfo = UpdateMediaInformation(collection, reader);
                        var rootName = Path.GetFileNameWithoutExtension(mediaItem.Path);
                        var allItems = MediaLibrary.GetMediaCollectionItems(collection.Id)
                                                   .Where(item =>
                                                   {
                                                       var result = Path.GetFileNameWithoutExtension(item.Name).StartsWith(rootName);
                                                       if (!result)
                                                           MediaLibrary.Release(item);
                                                       return result;
                                                   })
                                                   .ToList();
                        try
                        {
                            var nfoFile = allItems.FirstOrDefault(item => nfoExtensions.Contains(Path.GetExtension(item.Name)));
                            UpdateMediaInformation(mediaItem, nfoFile, reader);
                            UpdateMovie(mediaItem, collection, source);
                            UpdateTVShowEpisode(mediaItem, collection, source, parentMetaInfo);
                        }
                        finally
                        {
                            MediaLibrary.Release(allItems);
                        }
                    }
                    finally
                    {
                        MediaLibrary.Release(source);
                    }
                }
                finally
                {
                    MediaLibrary.Release(collection);
                }
            return true;
        }

        private void UpdateTVShowEpisode(MediaItem mediaItem, MediaCollection collection, MediaSource source, MediaInformation parentInfo)
        {
            var showInfo = parentInfo as TVShowInformation;
            var episodeInfo = mediaItem.MetaInformation as EpisodeInformation;
            var episode = MediaLibrary.GetTVShowEpisodeByMediaItem(mediaItem.Id);
            if ((episode is null) && (episodeInfo is null || showInfo is null))
                return;
            try
            {
                var show = UpdateShowByMediaItem(mediaItem, collection, source, showInfo);
                if (show is null)
                    return;
                try
                {
                    var season = UpdateSeasonByMediaItem(mediaItem, collection, source, show);
                    if (season is null) return;
                    try
                    {
                        if ((episode is not null) && (episodeInfo is null || showInfo is null))
                            DeactivateEpisode(episode);
                        else if (episodeInfo is not null)
                        {
                            var namedEpisode = MediaLibrary.GetTVShowEpisodeByIdentification(show.Name, episodeInfo.Season, episodeInfo.Episode, episodeInfo.Part);
                            if (namedEpisode is null)
                                episode = CreateEpisode(mediaItem, season);
                            else
                            {
                                MediaLibrary.Release(episode);
                                episode = UpdateEpisode(namedEpisode, mediaItem);
                            }
                        }
                        var isNew = episode.Id == 0;
                        episode.ShowName = show.Name;
                        episode.SeasonNo = season.Number;
                        mediaItem.NeedsPictureUpdate = true;
                        episode = MediaLibrary.AddOrUpdateEpisode(episode);
                        UpdateSeasonDescandantInformation(season);
                        UpdateShowDescandantInformation(show);
                        if (isNew)
                            Notify(this, new Events.NotificationEventArgs("EntryClassified-New", episode));
                        else
                            Notify(this, new Events.NotificationEventArgs("EntryClassified", episode));
                    }
                    finally
                    {
                        MediaLibrary.Release(season);
                    }
                }
                finally
                {
                    MediaLibrary.Release(show);
                }
            }
            finally
            {
                MediaLibrary.Release(episode);
            }
        }

        private void UpdateShowDescandantInformation(TVShow show)
        {
            var seasons = MediaLibrary.GetSeasons(show.Id).ToArray();
            try
            {
                show.ReleaseDate = seasons
                    .Where(e => e.ReleaseDate != DateTime.MinValue)
                    .Select(e => e.ReleaseDate)
                    .Aggregate(DateTime.MinValue, (minVal, nextVal) => (minVal < nextVal && minVal != DateTime.MinValue) ? minVal : nextVal);
                show.PremieredAt = seasons
                    .Where(e => e.PremieredAt != DateTime.MinValue)
                    .Select(e => e.PremieredAt)
                    .Aggregate(DateTime.MinValue, (minVal, nextVal) => (minVal < nextVal && minVal != DateTime.MinValue) ? minVal : nextVal);
                show = MediaLibrary.AddOrUpdateTVShow(show);
            }
            finally
            {
                MediaLibrary.Release(seasons);
            }
        }

        private void UpdateSeasonDescandantInformation(TVShowSeason season)
        {
            var episodes = MediaLibrary.GetEpisodes(season.Id).ToArray();
            try
            {
                season.ReleaseDate = episodes
                    .Where(e => e.ReleaseDate != DateTime.MinValue)
                    .Select(e => e.ReleaseDate)
                    .Aggregate(DateTime.MinValue, (minVal, nextVal) => (minVal < nextVal && minVal != DateTime.MinValue) ? minVal : nextVal);
                season.PremieredAt = episodes
                    .Where(e => e.PremieredAt != DateTime.MinValue)
                    .Select(e => e.PremieredAt)
                    .Aggregate(DateTime.MinValue, (minVal, nextVal) => (minVal < nextVal && minVal != DateTime.MinValue) ? minVal : nextVal);
                season = MediaLibrary.AddOrUpdateSeason(season);
            }
            finally { MediaLibrary.Release(episodes); }
        }

        private TVShowSeason UpdateSeasonByMediaItem(MediaItem mediaItem, MediaCollection collection, MediaSource source, TVShow show)
        {
            var episodeInfo = mediaItem.MetaInformation as EpisodeInformation;
            if (episodeInfo is null)
                return null;
            var season = MediaLibrary.GetShowSeason(show, episodeInfo.Season);
            if (season is null)
                season = CreateTVShowSeason(show, episodeInfo.Season);
            else
                season = UpdateTVShowSeason(season, episodeInfo.Season);
            season.ShowName = show.Name;
            var isNew = season.Id == 0;
            season = MediaLibrary.AddOrUpdateSeason(season);
            if (isNew)
                Notify(this, new Events.NotificationEventArgs("EntryClassified-New", season));
            else
                Notify(this, new Events.NotificationEventArgs("EntryClassified", season));
            return season;
        }

        private void UpdateTVShowSeasonPictures(TVShowSeason season, MediaCollection collection, MediaSource source)
        {
            var releaseCollection = false;
            while (collection.ParentId != 0)
            {
                var pictures = MediaLibrary.GetMediaCollectionItems(collection.Id)
                    .Where(i =>
                    {
                        var result = i.CopyType == MediaItemCopyType.Original;
                        result &= pictureExtensions.Contains(Path.GetExtension(i.Name));
                        if (!result)
                            MediaLibrary.Release(i);
                        return result;
                    })
                    .ToArray();
                try
                {
                    var poster = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"season{season.Number.ToString().PadLeft(2, '0')}-poster");
                    var banner = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"season{season.Number.ToString().PadLeft(2, '0')}-banner");
                    var fanart = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"season{season.Number.ToString().PadLeft(2, '0')}-fanart");
                    if (banner is not null || poster is not null)
                    {
                        if (banner is not null)
                        {
                            NotifyStatus($"Bereite Banner auf für: {season.ShowName} {season.ToString()}");
                            season.BannerPath = PreparePicture(source, collection, pictures, banner, season.BannerPath, season.BannerBackgroundColor, 300, true, out string backgroundColor);
                            season.BannerBackgroundColor = backgroundColor;
                        }
                        if (poster is not null)
                        {
                            NotifyStatus($"Bereite Poster auf für: {season.ShowName} {season.ToString()}");
                            season.PicturePath = PreparePicture(source, collection, pictures, poster, season.PicturePath, season.PictureBackgroundColor, 240, true, out string backgroundColor);
                            season.PictureBackgroundColor = backgroundColor;
                        }
                        else if (fanart is not null)
                        {
                            NotifyStatus($"Bereite Fanart auf für: {season.ShowName} {season.ToString()}");
                            season.PicturePath = PreparePicture(source, collection, pictures, fanart, season.PicturePath, season.PictureBackgroundColor, 240, true, out string backgroundColor);
                            season.PictureBackgroundColor = backgroundColor;
                        }
                        season = MediaLibrary.AddOrUpdateSeason(season);
                        break;
                    }
                    if (collection.MetaInformation is TVShowInformation)
                        break;
                    if (releaseCollection)
                        MediaLibrary.Release(collection);
                    collection = MediaLibrary.GetMediaCollection(collection.ParentId);
                    releaseCollection = true;
                }
                finally
                {
                    MediaLibrary.Release(pictures);
                }
            }
            if (releaseCollection)
                MediaLibrary.Release(collection);
        }

        private string PreparePicture(
            Models.Sources.MediaSource source, 
            MediaCollection collection, 
            MediaItem[] allPictures, 
            MediaItem pictureFile, 
            string currentpath, 
            string currentbackgroundColor,
            int maxHeight, 
            bool uploadThumb,
            out string backgroundColor)
        {
            backgroundColor = currentbackgroundColor;
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(pictureFile.Name);
            var expectedFileNameEnding = $"x{maxHeight}{Path.GetExtension(pictureFile.Name)}";
            foreach (var existingThumb in allPictures.Where(p =>
            {
                if (!p.Name.StartsWith(fileNameWithoutExtension))
                    return false;
                if (!p.Name.EndsWith(expectedFileNameEnding))
                    return false;
                return true;
            }))
                try
                {
                    var kV = (existingThumb is null)
                           ? UpdateCacheFileAsync(pictureFile, currentpath, source, 0, maxHeight, uploadThumb)
                           : UpdateCacheFileAsync(existingThumb, currentpath, source, 0, 0, false);
                    backgroundColor = kV.Value.ToHex();
                    return kV.Key;
                }
                catch
                {

                }

            try
            {
                var kV = UpdateCacheFileAsync(pictureFile, currentpath, source, 0, maxHeight, uploadThumb);
                backgroundColor = kV.Value.ToHex();
                return kV.Key;
            }
            catch
            {

            }

            return currentpath;
        }

        

        private TVShowSeason UpdateTVShowSeason(TVShowSeason season, int seasonNo)
        {
            return season;
        }

        private TVShowSeason CreateTVShowSeason(TVShow show, int seasonNo)
        {
            var season = new TVShowSeason(null)
            {
                Enabled = true,
                Visible = true,
                PicturePath = string.Empty,
                BannerPath = string.Empty,
                Number = seasonNo,
                ShowId = show.Id,
            };
            return UpdateTVShowSeason(season, seasonNo);
        }

        private TVShow UpdateShowByMediaItem(MediaItem mediaItem, MediaCollection collection, MediaSource source, TVShowInformation showInfo) {
            var episodeInfo = mediaItem.MetaInformation as EpisodeInformation;
            if (showInfo is null) 
                return null;
            var show = MediaLibrary.GetShowByName(showInfo.Title);
            if ((show is null) && (showInfo is null))
                return null;
            if (showInfo is not null)
            {
                if (show is null)
                    show = CreateTVShow(showInfo);
                else
                    show = UpdateTVShow(show, showInfo);
            }
            var isNew = show.Id == 0;
            show = MediaLibrary.AddOrUpdateTVShow(show);            
            ActivateGenres(show);
            if (isNew)
                Notify(this, new Events.NotificationEventArgs("EntryClassified-New", show));
            else
                Notify(this, new Events.NotificationEventArgs("EntryClassified", show));
            return show;
        }

        private void UpdateTVShowPictures(TVShow show, MediaCollection collection, MediaSource mediaSource)
        {
            var releaseCollection = false;
            while ((collection.ParentId != 0)
                && (collection.MetaInformation is null || collection.MetaInformation is not TVShowInformation))
            {
                if (releaseCollection)
                    MediaLibrary.Release(collection);
                collection = MediaLibrary.GetMediaCollection(collection.ParentId);
                releaseCollection = true;
            }
            if (releaseCollection)
                MediaLibrary.Release(collection);
            var pictures = MediaLibrary.GetMediaCollectionItems(collection.Id)
                .Where(i =>
                {
                    var result = i.CopyType == MediaItemCopyType.Original;
                    result &= pictureExtensions.Contains(Path.GetExtension(i.Name));
                    if (!result)
                        MediaLibrary.Release(i);
                    return result;
                })
                .ToArray();
            try
            {
                var poster = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"poster");
                var banner = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"banner");
                var fanart = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"fanart");
                var folder = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"folder");

                if (banner is not null)
                {
                    NotifyStatus($"Bereite Banner auf für: {show.Name}");
                    show.BannerPath = PreparePicture(mediaSource, collection, pictures, banner, show.BannerPath, show.BannerBackgroundColor, 300, true, out string backgroundColor);
                    show.BannerBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(show, $"Banner aktualisiert. (MediaItem {banner.Id} - {banner.Name})");
                }
                else if (fanart is not null)
                {
                    NotifyStatus($"Bereite Banner auf für: {show.Name}");
                    show.BannerPath = PreparePicture(mediaSource, collection, pictures, fanart, show.BannerPath, show.BannerBackgroundColor, 300, true, out string backgroundColor);
                    show.BannerBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(show, $"Banner aktualisiert. (MediaItem {fanart.Id} - {fanart.Name})");
                }
                if (poster is not null)
                {
                    NotifyStatus($"Bereite Poster auf für: {show.Name}");
                    show.PicturePath = PreparePicture(mediaSource, collection, pictures, poster, show.PicturePath, show.PictureBackgroundColor, 240, true, out string backgroundColor);
                    show.PictureBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(show, $"Bild aktualisiert. (MediaItem {poster.Id} - {poster.Name})");
                }
                else if (folder is not null)
                {
                    NotifyStatus($"Bereite Poster auf für: {show.Name}");
                    show.PicturePath = PreparePicture(mediaSource, collection, pictures, folder, show.PicturePath, show.PictureBackgroundColor, 240, true, out string backgroundColor);
                    show.PictureBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(show, $"Bild aktualisiert. (MediaItem {folder.Id} - {folder.Name})");
                }
            }
            finally
            {
                MediaLibrary.Release(pictures);
            }
            show = MediaLibrary.AddOrUpdateTVShow(show);
        }

        private TVShow UpdateTVShow(TVShow show, TVShowInformation showInfo)
        {
            show.Name = showInfo.Title;
            show.OriginalName = showInfo.OriginalTitle;
            show.Language = showInfo.Language;
            show.Plot = showInfo.Plot;
            show.Genres = showInfo.Genres;
            return show;
        }

        private TVShow CreateTVShow(TVShowInformation showInfo)
        {
            var show = new TVShow(null)
            {
                Enabled = true,
                Visible = true,
                PicturePath = string.Empty,
                BannerPath = string.Empty
            };
            return UpdateTVShow(show, showInfo);
        }

        private void UpdateEpisodePictures(TVShowEpisode episode, MediaItem mediaItem, MediaCollection collection, MediaSource mediaSource)
        {
            var rootName = Path.GetFileNameWithoutExtension(mediaItem.Name);
            var pictures = MediaLibrary.GetMediaCollectionItems(collection.Id)
                .Where(i =>
                {
                    var result = i.CopyType == MediaItemCopyType.Original;
                    result &= i.Name.StartsWith(rootName);
                    result &= pictureExtensions.Contains(Path.GetExtension(i.Name));
                    if (!result)
                        MediaLibrary.Release(i);
                    return result;
                })
                .ToArray();
            try
            {
                var poster = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}-poster");
                var banner = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}-banner");
                var fanart = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}-fanart");
                var thumb = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}-thumb");

                if (banner is not null)
                {
                    NotifyStatus($"Bereite Banner auf für: {episode.Name}");
                    episode.BannerPath = PreparePicture(mediaSource, collection, pictures, banner, episode.BannerPath, episode.BannerBackgroundColor, 300, true, out string backgroundColor);
                    episode.BannerBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(episode, $"Banner aktualisiert. (MediaItem {banner.Id} - {banner.Name})");
                }
                else if (fanart is not null)
                {
                    NotifyStatus($"Bereite Fanart auf für: {episode.Name}");
                    episode.BannerPath = PreparePicture(mediaSource, collection, pictures, fanart, episode.BannerPath, episode.BannerBackgroundColor, 300, true, out string backgroundColor);
                    episode.BannerBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(episode, $"Banner aktualisiert. (MediaItem {fanart.Id} - {fanart.Name})");
                }
                if (poster is not null)
                {
                    NotifyStatus($"Bereite Poster auf für: {episode.Name}");
                    episode.PicturePath = PreparePicture(mediaSource, collection, pictures, poster, episode.PicturePath, episode.PictureBackgroundColor, 240, true, out string backgroundColor);
                    episode.PictureBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(episode, $"Bild aktualisiert. (MediaItem {poster.Id} - {poster.Name})");
                }
                else if (thumb is not null)
                {
                    NotifyStatus($"Bereite Thumb auf für: {episode.Name}");
                    episode.PicturePath = PreparePicture(mediaSource, collection, pictures, thumb, episode.PicturePath, episode.PictureBackgroundColor, 240, true, out string backgroundColor);
                    episode.PictureBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(episode, $"Bild aktualisiert. (MediaItem {thumb.Id} - {thumb.Name})");
                }
                else if (fanart is not null)
                {
                    NotifyStatus($"Bereite Fanart auf für: {episode.Name}");
                    episode.PicturePath = PreparePicture(mediaSource, collection, pictures, fanart, episode.PicturePath, episode.PictureBackgroundColor, 240, true, out string backgroundColor);
                    episode.PictureBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(episode, $"Bild aktualisiert. (MediaItem {fanart.Id} - {fanart.Name})");
                }
            }
            finally
            {
                MediaLibrary.Release(pictures);
            }
            MediaLibrary.AddOrUpdateEpisode(episode);
        }

        private TVShowEpisode UpdateEpisode(TVShowEpisode episode, MediaItem mediaItem)
        {
            var episodeInfo = mediaItem.MetaInformation as EpisodeInformation;
            episode.Name = episodeInfo.Title;
            episode.Language = episodeInfo.Language;
            episode.OriginalName = episodeInfo.OriginalTitle;
            episode.Episode = episodeInfo.Episode;
            episode.Part = episodeInfo.Part;
            episode.Plot = episodeInfo.Plot;
            episode.MediaItemIds = episode.MediaItemIds.Concat(new long[] { mediaItem.Id }).Distinct().ToArray();
            episode.PremieredAt = episodeInfo.AiredAt;
            episode.ReleaseDate = episodeInfo.AiredAt;
            episode.Enabled = true;
            episode.Visible = true;
            return episode;
        }

        private TVShowEpisode CreateEpisode(MediaItem mediaItem, TVShowSeason season)
        {
            var episodeInfo = mediaItem.MetaInformation as EpisodeInformation;
            var episode = new TVShowEpisode(null)
            {
                Enabled = true,
                Visible = true,
                PicturePath = string.Empty,
                BannerPath = string.Empty,
                SeasonId = season.Id,
                DownloadMediaItemId = 0,
            };
            return UpdateEpisode(episode, mediaItem);
        }

        private void DeactivateEpisode(TVShowEpisode episode)
        {
            episode.Enabled = false;
        }

        private void DeactivateMovie(Movie movie)
        {
            movie.Enabled = false;
        }

        private Movie CreateMovie(MediaItem mediaItem)
        {
            var movieInfo = mediaItem.MetaInformation as MovieInformation;
            var movie = new Movie(null)
            {
                Enabled = true,
                Visible = true,
                IsSingle = true,
                PicturePath = string.Empty,
                BannerPath = string.Empty,
                CollectionId = 0,
                TrailerMediaItemId = 0,
                DownloadMediaItemId = 0,
            };
            return UpdateMovie(movie, mediaItem);
        }

        private Movie UpdateMovie(Movie movie, MediaItem mediaItem)
        {
            var movieInfo = mediaItem.MetaInformation as MovieInformation;
            movie.Name = movieInfo.Title;
            movie.OriginalTitle = movieInfo.OriginalTitle;
            movie.Language = movieInfo.Language;
            movie.ReleaseDate = movieInfo.ReleaseDate;
            movie.PremieredAt = movieInfo.PremieredAt;
            movie.Plot = movieInfo.Plot;
            movie.Genres = movieInfo.Genres;
            movie.MediaItemIds = movie.MediaItemIds.Concat(new long[] { mediaItem.Id }).Distinct().ToArray();
            movie.Enabled = true;
            movie.Visible = true;
            movie.Director = movieInfo.Director;
            return movie;
        }

        private MovieCollection CreateMovieCollection(Movie movie, MediaItem mediaItem)
        {
            var collection = new MovieCollection(null)
            {
                Enabled = true,
                MediaItemCollectionId = mediaItem.ParentCollectionId,
                Genres = new string[0]
            };
            return UpdateMovieCollection(collection, movie, mediaItem);
        }

        
        private MovieCollection UpdateMovieCollection(MovieCollection collection, Movie movie, MediaItem mediaItem)
        {
            var collectionMovies = MediaLibrary
                .GetCollectionMovies(collection.Id)
                .ToArray();

            var commonName = collectionMovies.Select(m => m.Name).ToArray().LongestCommonPrefix().TrimEnd();
            if (string.IsNullOrWhiteSpace(commonName))
            {
                var mediaCollection = MediaLibrary.GetMediaCollection(mediaItem.ParentCollectionId);
                commonName = mediaCollection?.Name;
                if (string.IsNullOrWhiteSpace(commonName))
                    commonName = movie.Name;
            }                

            collection.Enabled = collectionMovies.Any(m => m.Enabled);            
            collection.Name = commonName;
            if (collectionMovies.Any())
            {
                collection.ReleaseDate = collectionMovies.Min(m => m.ReleaseDate);
                collection.PremieredAt = collectionMovies.Min(m => m.PremieredAt);
            }
            collection.PicturePath = movie.PicturePath;
            collection.PictureBackgroundColor = movie.PictureBackgroundColor;
            collection.IsSingle = collectionMovies.Count(m => m.Enabled) <= 1;
            collection.Visible = !collection.IsSingle && collection.Enabled;
            collection.BannerPath = movie.BannerPath;
            collection.BannerBackgroundColor = movie.BannerBackgroundColor;
            collection.Genres = collectionMovies.SelectMany(m => m.Genres).Distinct().OrderBy(g => g).ToArray();
            return collection;
        }

        private void UpdateMovieCollection(Movie movie, MediaItem mediaItem)
        {
            var collectionMovies = MediaLibrary
                .GetMediaCollectionItems(mediaItem.ParentCollectionId)
                .Where(mi =>
                {
                    var result = mi.CopyType == MediaItemCopyType.Original;
                    result &= videoExtensions.Contains(Path.GetExtension(mi.Name));
                    result &= !trailerExtensions.Any(ext => mi.Name.EndsWith(ext));
                    MediaLibrary.Release(mi);
                    return result;
                })
                .Select(mi => Path.GetFileNameWithoutExtension(mi.Name))
                .Distinct()
                .ToArray();
            movie.IsSingle = collectionMovies.Count() == 1;
            movie.Visible = movie.IsSingle && movie.Enabled;

            var movieCollection = MediaLibrary.GetMovieCollection(movie.CollectionId);
            try
            {
                if (movieCollection is null)
                    movieCollection = MediaLibrary.GetMovieCollectionByMediaCollection(mediaItem.ParentCollectionId);
                if (movieCollection is null)
                    movieCollection = CreateMovieCollection(movie, mediaItem);
                else
                    movieCollection = UpdateMovieCollection(movieCollection, movie, mediaItem);
                var isNew = movieCollection.Id == 0;
                movieCollection = MediaLibrary.AddOrUpdateMovieCollection(movieCollection);
                movie.CollectionId = movieCollection.Id;
                movie = MediaLibrary.AddOrUpdateMovie(movie);
                UpdateCollectionMovies(movieCollection);
                if (isNew)
                    Notify(this, new Events.NotificationEventArgs("EntryClassified-New", movieCollection));
                else
                    Notify(this, new Events.NotificationEventArgs("EntryClassified", movieCollection));
            }
            finally
            {
                MediaLibrary.Release(movieCollection);
            }            
        }

        private void UpdateCollectionMovies(MovieCollection collection)
        {
            var collectionMovies = MediaLibrary
                .GetCollectionMovies(collection.Id)
                .ToArray();
            try
            {
                foreach (var movie in collectionMovies
                    .Where(movie => movie.IsSingle != collection.IsSingle))
                {
                    movie.IsSingle = collection.IsSingle;
                    _ = MediaLibrary.AddOrUpdateMovie(movie);
                }
            }
            finally
            {
                MediaLibrary.Release(collectionMovies);
            }
        }

        private void UpdateMovie(MediaItem mediaItem, MediaCollection collection, MediaSource mediaSource)
        {
            var movieInfo = mediaItem.MetaInformation as MovieInformation;
            var movie = MediaLibrary.GetMovieByMediaItem(mediaItem.Id);
            if ((movie is null) && (movieInfo is null))
                return;
            try
            {
                if (mediaItem.CopyType == MediaItemCopyType.Download)
                    AddDownloadToMovie(movie, mediaItem);
                else if (mediaItem.CopyType == MediaItemCopyType.Cache)
                    AddCacheToMovie(movie, mediaItem);
                else if ((movie is not null) && (movieInfo is null))
                    DeactivateMovie(movie);
                else if (movieInfo is not null)
                {
                    var namedMovies = MediaLibrary.GetMoviesByName(movieInfo.Title).ToList();
                    namedMovies = namedMovies
                        .Where(movie => {
                            var date = movieInfo.ReleaseDate == DateTime.MinValue ? movieInfo.PremieredAt : movieInfo.ReleaseDate;
                            var result = date == DateTime.MinValue || movie.ReleaseDate == date || movie.PremieredAt == date;
                            if (!result)
                                MediaLibrary.Release(movie);
                            return result;
                        })
                        .OrderBy(movie => (movieInfo.Language == movie.Language) ? 0 : 1)
                        .ThenBy(movie => movie.Name)
                        .ToList();
                    var namedMovie = namedMovies.FirstOrDefault();                                        
                    if (namedMovie is null && movie is null)
                        movie = CreateMovie(mediaItem);
                    else if (movie is null)
                    {
                        MediaLibrary.Release(namedMovies.Skip(1));
                        MediaLibrary.Release(movie);
                        movie = UpdateMovie(namedMovie, mediaItem);
                    }
                    else
                    {
                        MediaLibrary.Release(namedMovies);
                        movie = UpdateMovie(movie, mediaItem);
                    }
                }
                var isNew = movie.Id == 0;
                mediaItem.NeedsPictureUpdate = true;
                movie = MediaLibrary.AddOrUpdateMovie(movie);
                MediaLibrary.AddProtocol(movie, $"Klassifiziert (durch MediaItem {mediaItem.Id} - {mediaItem.Name}).");
                UpdateMovieCollection(movie, mediaItem);
                ActivateGenres(movie);
                UpdateMovieActors(movie, movieInfo);
                if (isNew)
                    Notify(this, new Events.NotificationEventArgs("EntryClassified-New", movie));
                else
                    Notify(this, new Events.NotificationEventArgs("EntryClassified", movie));
            }
            finally
            {
                MediaLibrary.Release(movie);
            }
        }

        private void UpdateMovieActors(Movie movie, MovieInformation movieInfo)
        {
            if (movieInfo is null) return;
            List<Actor> actors = new List<Actor>();
            try
            {
                var roles = movieInfo.Actors is null ? new Role[0] : movieInfo.Actors.Select(actorInfo =>
                {
                    Actor actor = GetOrCreateActor(actorInfo);
                    actors.Add(actor);
                    return new Role(null)
                    {
                        Actor = actor,
                        ActorId = actor.Id,
                        Name = actorInfo.Role,
                        EntryId = movie.Id
                    };
                }).ToArray();

                var existingRoles = MediaLibrary.GetRoles(movie.Id)
                    .Where(role => role is not null)
                    .OrderBy(r => r.Id)
                    .ToList();
                try
                {
                    var rolesToSave = roles.Select(role =>
                    {
                        var existing = existingRoles.FirstOrDefault();
                        if (existing is null)
                            return role;
                        existingRoles.RemoveAt(0);
                        existing.Actor = role.Actor;
                        existing.ActorId = role.ActorId;
                        existing.Name = role.Name;
                        existing.EntryId = role.EntryId;
                        return existing;
                    });
                    try
                    {
                        foreach (var role in rolesToSave)
                            MediaLibrary.AddOrUpdateRole(role);
                        foreach (var role in existingRoles)
                            MediaLibrary.Delete(role);
                    }
                    finally
                    {
                        MediaLibrary.Release(rolesToSave);
                    }
                }
                finally
                {
                    MediaLibrary.Release(existingRoles);
                }


            }
            finally
            {
                UpdateActorRoleCounts(actors);
                MediaLibrary.Release(actors);
            }
        }

        private void UpdateActorRoleCounts(List<Actor> actors)
        {
            foreach (var actor in actors)
                UpdateActorRoleCount(actor);
        }

        private void UpdateActorRoleCount(Actor actor)
        {
            actor.RoleCountUpdated = false;
            MediaLibrary.AddOrUpdateActor(actor);
        }

        private Actor GetOrCreateActor(ActorInformation actorInfo)
        {
            var actors = MediaLibrary.GetActorsByName(actorInfo.Name).ToList();
            try
            {
                var actor = actors.FirstOrDefault(act => act.ThumbUri == actorInfo.Thumb);
                if (actor is null)
                    actor = CreateActor(actorInfo);
                else
                    actors.Remove(actor);
                return actor;
            }
            finally
            {
                MediaLibrary.Release(actors);
            }            
        }

        private Actor CreateActor(ActorInformation actorInfo)
        {
            Actor actor = new Actor(null)
            {
                Name = actorInfo.Name,
                ThumbUri = actorInfo.Thumb,
                NeedsPictureUpdate = true
            };
            actor = MediaLibrary.AddOrUpdateActor(actor);            
            return actor;
        }

        private void ActivateGenres(Movie movie)
        {
            foreach (var genre in movie.Genres.Select(g => MediaLibrary.GetGenres()
                .FirstOrDefault(genre =>
                {
                    var result = genre.Name == g;
                    if (!result)
                        MediaLibrary.Release(genre);
                    return result;
                })))
                try
                {
                    ActivateGenre(genre, true, false);
                }
                finally
                {
                    MediaLibrary.Release(genre);
                }
        }
        private void ActivateGenres(TVShow show)
        {
            foreach (var genre in show.Genres.Select(g => MediaLibrary.GetGenres()
                .FirstOrDefault(genre =>
                {
                    var result = genre.Name == g;
                    if (!result)
                        MediaLibrary.Release(genre);
                    return result;
                })))
                try
                {
                    ActivateGenre(genre, false, true);
                }
                finally
                {
                    MediaLibrary.Release(genre);
                }
        }

        private void ActivateGenre(Genre genre, bool movie, bool tvshow)
        {
            bool changed = false;
            if (movie)
                changed = genre.HasMovies = true;
            if (tvshow)
                changed = genre.HasTVShow = true;
            if (changed)
                MediaLibrary.AddOrUpdateGenre(genre);
        }

        private void AddCacheToMovie(Movie movie, MediaItem mediaItem)
        {
            movie.MediaItemIds = movie.MediaItemIds.Concat(new long[] { mediaItem.Id }).Distinct().ToArray();
            if (movie.DownloadMediaItemId == 0)
                movie.DownloadMediaItemId = mediaItem.Id;
        }

        private void AddDownloadToMovie(Movie movie, MediaItem mediaItem)
        {
            movie.MediaItemIds = movie.MediaItemIds.Concat(new long[] { mediaItem.Id}).Distinct().ToArray();
            movie.DownloadMediaItemId = mediaItem.Id;
        }

        private void UpdateMoviePictures(Movie movie, MediaItem mediaItem, MediaCollection collection, MediaSource mediaSource)
        {
            var rootName = Path.GetFileNameWithoutExtension(mediaItem.Name);
            var pictures = MediaLibrary.GetMediaCollectionItems(collection.Id)
                .Where(i =>
                {
                    var result = i.CopyType == MediaItemCopyType.Original;
                    result &= i.Name.StartsWith(rootName);
                    result &= pictureExtensions.Contains(Path.GetExtension(i.Name));
                    if (!result)
                        MediaLibrary.Release(i);
                    return result;
                })
                .ToArray();
            try
            {
                var filmPicture = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}");
                var poster = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}-poster");
                var banner = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}-banner");
                var fanart = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}-fanart");
                var landscape = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"{rootName}-landscape");

                if (banner is not null)
                {
                    NotifyStatus($"Bereite Banner auf für: {movie.Name}");
                    movie.BannerPath = PreparePicture(mediaSource, collection, pictures, banner, movie.BannerPath, movie.BannerBackgroundColor, 300, true, out string backgroundColor);
                    movie.BannerBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(movie, $"Banner aktualisiert. (MediaItem {banner.Id} - {banner.Name})");
                }
                else if (fanart is not null)
                {
                    NotifyStatus($"Bereite Banner auf für: {movie.Name}");
                    movie.BannerPath = PreparePicture(mediaSource, collection, pictures, fanart, movie.BannerPath, movie.BannerBackgroundColor, 300, true, out string backgroundColor);
                    movie.BannerBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(movie, $"Banner aktualisiert. (MediaItem {fanart.Id} - {fanart.Name})");
                }
                else if (landscape is not null)
                {
                    NotifyStatus($"Bereite Banner auf für: {movie.Name}");
                    movie.BannerPath = PreparePicture(mediaSource, collection, pictures, landscape, movie.BannerPath, movie.BannerBackgroundColor, 300, true, out string backgroundColor);
                    movie.BannerBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(movie, $"Banner aktualisiert. (MediaItem {landscape.Id} - {landscape.Name})");
                }
                if (poster is not null)
                {
                    NotifyStatus($"Bereite Poster auf für: {movie.Name}");
                    movie.PicturePath = PreparePicture(mediaSource, collection, pictures, poster, movie.PicturePath, movie.PictureBackgroundColor, 240, true, out string backgroundColor);
                    movie.PictureBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(movie, $"Bild aktualisiert. (MediaItem {poster.Id} - {poster.Name})");
                }
                else if (filmPicture is not null)
                {
                    NotifyStatus($"Bereite Poster auf für: {movie.Name}");
                    movie.PicturePath = PreparePicture(mediaSource, collection, pictures, filmPicture, movie.PicturePath, movie.PictureBackgroundColor, 240, true, out string backgroundColor);
                    movie.PictureBackgroundColor = backgroundColor;
                    MediaLibrary.AddProtocol(movie, $"Bild aktualisiert. (MediaItem {filmPicture.Id} - {filmPicture.Name})");
                }

                var movieCollection = MediaLibrary.GetMovieCollection(movie.CollectionId);
                try
                {
                    UpdateMovieCollectionPicturesAsync(movieCollection, collection, mediaSource);
                }
                finally
                {
                    MediaLibrary.Release(movieCollection);
                }
            }
            finally
            {
                MediaLibrary.Release(pictures);
            }
            MediaLibrary.AddOrUpdateMovie(movie);
        }


       private void UpdateMovieCollectionPicturesAsync(MovieCollection movieCollection, MediaCollection collection, MediaSource mediaSource)
        {
            var pictures = MediaLibrary.GetMediaCollectionItems(collection.Id)
                .Where(i =>
                {
                    var result = i.CopyType == MediaItemCopyType.Original;
                    result &= pictureExtensions.Contains(Path.GetExtension(i.Name));
                    if (!result)
                        MediaLibrary.Release(i);
                    return result;
                })
                .ToArray();
            try
            {
                var movies = MediaLibrary.GetCollectionMovies(movieCollection.Id).ToArray();
                try
                {                    
                    var poster = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"poster");
                    var banner = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"banner");
                    var fanart = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"fanart");
                    var folder = pictures.FirstOrDefault(i => Path.GetFileNameWithoutExtension(i.Name) == $"folder");

                    if (banner is not null)
                    {
                        NotifyStatus($"Bereite Banner auf für: {movieCollection.Name}");
                        movieCollection.BannerPath = PreparePicture(mediaSource, collection, pictures, banner, movieCollection.BannerPath, movieCollection.BannerBackgroundColor, 300, true, out string backgroundColor);
                        movieCollection.BannerBackgroundColor = backgroundColor;
                        MediaLibrary.AddProtocol(movieCollection, $"Banner aktualisiert. (MediaItem {banner.Id} - {banner.Name})");
                    }
                    else if (fanart is not null)
                    {
                        NotifyStatus($"Bereite Banner auf für: {movieCollection.Name}");
                        movieCollection.BannerPath = PreparePicture(mediaSource, collection, pictures, fanart, movieCollection.BannerPath, movieCollection.BannerBackgroundColor, 300, true, out string backgroundColor);
                        movieCollection.BannerBackgroundColor = backgroundColor;
                        MediaLibrary.AddProtocol(movieCollection, $"Banner aktualisiert. (MediaItem {fanart.Id} - {fanart.Name})");
                    }
                    else
                    {
                        var firstMovie = movies
                            .Where(m => !string.IsNullOrWhiteSpace(m.BannerPath))
                            .OrderBy(m => m.ReleaseDate)
                            .ThenBy(m => m.PremieredAt)
                            .FirstOrDefault();
                        if (firstMovie is not null)
                        {
                            movieCollection.BannerBackgroundColor = firstMovie.BannerBackgroundColor;
                            movieCollection.BannerPath = firstMovie.BannerPath;
                            MediaLibrary.AddProtocol(movieCollection, $"Banner von Film aktualisiert. (Film {firstMovie.Id} - {firstMovie.Name})");
                        }
                    }

                    if (poster is not null)
                    {
                        NotifyStatus($"Bereite Poster auf für: {movieCollection.Name}");
                        movieCollection.PicturePath = PreparePicture(mediaSource, collection, pictures, poster, movieCollection.BannerPath, movieCollection.BannerBackgroundColor, 240, true, out string backgroundColor);
                        movieCollection.PictureBackgroundColor = backgroundColor;
                        MediaLibrary.AddProtocol(movieCollection, $"Bild aktualisiert. (MediaItem {poster.Id} - {poster.Name})");
                    }
                    else if (folder is not null)
                    {
                        NotifyStatus($"Bereite Poster auf für: {movieCollection.Name}");
                        movieCollection.PicturePath = PreparePicture(mediaSource, collection, pictures, folder, movieCollection.BannerPath, movieCollection.BannerBackgroundColor, 240, true, out string backgroundColor);
                        movieCollection.PictureBackgroundColor = backgroundColor;
                        MediaLibrary.AddProtocol(movieCollection, $"Bild aktualisiert. (MediaItem {folder.Id} - {folder.Name})");
                    }
                    else
                    {
                        var firstMovie = movies
                            .Where(m => !string.IsNullOrWhiteSpace(m.PicturePath))
                            .OrderBy(m => m.ReleaseDate)
                            .ThenBy(m => m.PremieredAt)
                            .FirstOrDefault();
                        if (firstMovie is not null)
                        {
                            movieCollection.PictureBackgroundColor = firstMovie.PictureBackgroundColor;
                            movieCollection.PicturePath = firstMovie.PicturePath;
                            MediaLibrary.AddProtocol(movieCollection, $"Bild von Film übernommen. (Film {firstMovie.Id} - {firstMovie.Name})");
                        }
                    }
                    MediaLibrary.AddOrUpdateMovieCollection(movieCollection);
                }
                finally
                {
                    MediaLibrary.Release(movies);
                }
            }
            finally
            {
                MediaLibrary.Release(pictures);
            }
        }
        private KeyValuePair<string, Microsoft.Maui.Graphics.Color> UpdateCacheFileAsync(MediaItem mediaItem, string destPath, MediaSource mediaSource, int width, int height, bool uploadThumb)
        {
            var pictureBackgroundColor = Colors.Transparent;
            if (string.IsNullOrEmpty(destPath))
            {
                var cacheFolder = PathTools.Combine(FileSystem.Current.AppDataDirectory, "Cache");
                destPath = PathTools.Combine(cacheFolder, $"{Guid.NewGuid()}{Path.GetExtension(mediaItem.Name)}");
            }
            else 
                destPath = PathTools.Combine(FileSystem.Current.AppDataDirectory, destPath);

            var reader = CreateReader(mediaSource);
            var sourceFileInfo = reader.ReadFile(mediaItem);
            var destFileInfo = new FileInfo(destPath);
            if (!destFileInfo.Directory.Exists)
                destFileInfo.Directory.Create();
            if (!destFileInfo.Exists || destFileInfo.LastWriteTime < sourceFileInfo.LastWriteTime)
            {
                var tempfile = reader.Download(mediaItem, (p) => { });
                if (tempfile.Exists)
                    tempfile.MoveTo(destFileInfo.FullName, true);
            }
            destFileInfo.Refresh();
            if (!destFileInfo.Exists)
                return new KeyValuePair<string, Microsoft.Maui.Graphics.Color>(String.Empty, Colors.Transparent);
            try
            {
                using (var image = Image.Load<Rgba32>(destFileInfo.FullName))
                {
                    pictureBackgroundColor = image.GetPixelColor(0, 0);
                    if (height != 0)
                        if (height < image.Height)
                        {
                            image.Mutate(i => i.Resize(0, height));
                            image.Save(destFileInfo.FullName);

                            if (uploadThumb)
                            {
                                var thumbFileName = $"{Path.GetFileNameWithoutExtension(mediaItem.Name)}-{image.Width}x{image.Height}{Path.GetExtension(mediaItem.Name)}";
                                reader.Upload(destFileInfo.FullName, $"{Path.GetDirectoryName(mediaItem.Path)}/{thumbFileName}", (p) => { NotifyStatus($"Lade aufbereitetes Bild hoch: {p}%"); });
                            }
                        }
                }
                return new KeyValuePair<string, Microsoft.Maui.Graphics.Color>(destFileInfo.FullName.Remove(0, FileSystem.Current.AppDataDirectory.Length), pictureBackgroundColor);
            }
            catch (InvalidImageContentException ex)
            {
                Debug.WriteLine(ex.ToString());
                if (destFileInfo.Exists)
                    destFileInfo.Delete();
                throw;
            }
        }

        private MediaInformation UpdateMediaInformation(MediaCollection collection, ISourceReader reader)
        {
            var nfoFile = MediaLibrary.GetMediaCollectionItems(collection.Id)                                       
                                        .FirstOrDefault(item =>
                                        {
                                            var result= item.Name == infoFileTVShow;
                                            if (!result)
                                                MediaLibrary.Release(item, true);
                                            return result;
                                        });
            try
            {
                MediaInformation returnInfo = null;
                if (nfoFile is not null)
                {
                    returnInfo = ReadMediaInformation(nfoFile, reader);
                    collection.MetaInformation = returnInfo;
                    collection.LastMetaInformationUpdate = DateTime.Now;
                    MediaLibrary.AddOrUpdateMediaCollection(collection);
                }
                if (collection.ParentId != 0)
                {
                    collection = MediaLibrary.GetMediaCollection(collection.ParentId);
                    if (collection is not null)
                        try
                        {
                            var parentInfo = UpdateMediaInformation(collection, reader);
                            if (returnInfo is null)
                                returnInfo = parentInfo;
                        }
                        finally
                        {
                            MediaLibrary.Release(collection, true);
                        }
                }
                return returnInfo;
            }
            finally
            {
                MediaLibrary.Release(nfoFile);
            }            
        }
        private void UpdateMediaInformation(MediaItem mediaItem, MediaItem nfoFile, ISourceReader reader)
        {
            var info = ReadMediaInformation(nfoFile, reader);
            mediaItem.MetaInformation = info;
            mediaItem.LastMetaInformationUpdate = DateTime.Now;
        }

        private MediaInformation ReadMediaInformation(MediaItem nfoFile, ISourceReader reader)
        {
            if (nfoFile is null)
                return null;
            XmlDocument XmlDoc = new XmlDocument();
            try
            {
                XmlDoc.LoadXml(reader.ReadTextFile(nfoFile));
                if (XmlDoc.DocumentElement == null)
                    return null;

                return XmlDoc.DocumentElement.Name switch
                {
                    "movie" => CreateMovieInformation(XmlDoc.DocumentElement),
                    "episodedetails" => CreateEpisodeInformation(XmlDoc.DocumentElement, nfoFile.Name),
                    "tvshow" => CreateTVShowInformation(XmlDoc.DocumentElement),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }

        private TVShowInformation CreateTVShowInformation(XmlElement documentElement)
        {
            TVShowInformation info = new TVShowInformation()
            {
                Title = documentElement.FindChild("title", true).InnerText.Trim(),
                OriginalTitle = "",
                Plot = documentElement.FindChild("plot", true).InnerText.Trim(),
                Genres = documentElement.FindChildren("genre").Select(node => node.InnerText.Trim()).ToArray(),
                PremieredAt = documentElement.FindChild("premiered", true).InnerText.ToDateTime(),
                Language = documentElement.FindChild("language", true).InnerText.Trim(),
                Studios = documentElement.FindChildren("studio").Select(node => node.InnerText).ToArray(),
                Actors = documentElement.FindChildren("actor").Select(node => new ActorInformation()
                {
                    Name = node.FindChild("name", true).InnerText.Trim(),
                    Role = node.FindChild("role", true).InnerText.Trim(),
                    Order = node.FindChild("order", true).InnerText.Trim().ToInt32(),
                    Thumb = node.FindChild("thumb", true).InnerText.Trim(),
                }).ToArray(),
            };
            info.Genres = CorrectGenres(info.Genres);
            return info;
        }

        private string[] CorrectGenres(string[] genres)
        {
            var allGenres = MediaLibrary.GetGenres().ToArray();
            try
            {
                for (int idx = genres.GetLowerBound(0); idx <= genres.GetUpperBound(0); idx++)
                {
                    var genre = genres[idx];
                    var origGenre = allGenres.FirstOrDefault(g => g.Name == genre);
                    var altGenre = origGenre ?? allGenres.FirstOrDefault(g => g.AlternateNames.Any(ga => ga.Name == genre));
                    if (altGenre is null)
                        altGenre = MediaLibrary.AddOrUpdateGenre(Genre.Create(genre));
                    genres[idx] = altGenre.Name;
                }
            }
            finally
            {
                MediaLibrary.Release(allGenres, false);
            }
            return genres.Distinct().ToArray();
        }

        private MovieInformation CreateMovieInformation(XmlElement xmlNode)
        {
            MovieInformation info = new MovieInformation()
            {
                Title = xmlNode.FindChild("title", true).InnerText.Trim(),
                OriginalTitle = xmlNode.FindChild("originaltitle", true).InnerText.Trim(),
                Genres = xmlNode.ChildNodes
                                .OfType<XmlNode>()
                                .Where(n => n.Name.ToLower() == "genre")
                                .Select(n => n.InnerText.Trim())
                                .ToArray(),
                Plot = xmlNode.FindChild("plot", true).InnerText.Trim(),
                ReleaseDate = xmlNode.FindChild("releasedate", true).InnerText.Trim().ToDateTime(),
                PremieredAt = xmlNode.FindChild("premiered", true).InnerText.Trim().ToDateTime(),
                Year = xmlNode.FindChild("year", true).InnerText.Trim().ToInt32(),
                LastUpdate = DateTime.Now,
                Actors = xmlNode.FindChildren("actor").Select(node => new ActorInformation()
                {
                    Name = node.FindChild("name", true).InnerText.Trim(),
                    Role = node.FindChild("role", true).InnerText.Trim(),
                    Order = node.FindChild("order", true).InnerText.Trim().ToInt32(),
                    Thumb = node.FindChild("thumb", true).InnerText.Trim(),
                }).ToArray(),
                Studios = xmlNode.FindChildren("studio").Select(node => node.InnerText).ToArray(),
                Director = xmlNode.FindChild("director", true).InnerText,
                Language = xmlNode.FindChild("language", true).InnerText,
            };
            if ((info.Year == 0) && (info.ReleaseDate != default))
                info.Year = info.ReleaseDate.Year;
            info.Genres = CorrectGenres(info.Genres);
            return info;
        }

        private EpisodeInformation CreateEpisodeInformation(XmlElement xmlNode, string itemName)
        {
            Regex regex = new Regex("\\((\\d+)\\)");
            Match match = regex.Match(itemName);

            var Info = new EpisodeInformation()
            {
                Title = xmlNode.FindChild("title", true).InnerText.Trim(),
                ShowName = xmlNode.FindChild("showname", true).InnerText.Trim(),
                Episode = int.Parse(xmlNode.FindChild("episode", true).InnerText.Trim()),
                Part = match.Success ? match.Groups[1].Value : string.Empty,
                Season = int.Parse(xmlNode.FindChild("season", true).InnerText.Trim()),
                Plot = xmlNode.FindChild("plot", true).InnerText.Trim(),
                LastUpdate = DateTime.Now,
                OriginalTitle = "",
                Language = "",
                AiredAt = xmlNode.FindChild("aired", true).InnerText.Trim().ToDateTime(),
                Actors = xmlNode.FindChildren("actor").Select(node => new ActorInformation()
                {
                    Name = node.FindChild("name", true).InnerText.Trim(),
                    Role = node.FindChild("role", true).InnerText.Trim(),
                    Order = node.FindChild("order", true).InnerText.Trim().ToInt32(),
                    Thumb = node.FindChild("thumb", true).InnerText.Trim(),
                }).ToArray(),
                Director = xmlNode.FindChild("director", true).InnerText
            };
            return Info;
        }

        private bool ClassifyGeneralFile(MediaItem mediaItem)
        {
            var rootName = Path.GetFileNameWithoutExtension(mediaItem.Path);
            var collection = MediaLibrary.GetMediaCollection(mediaItem.ParentCollectionId);
            IEnumerable<MediaItem> items = new MediaItem[0];
            if (collection is not null)
                try
                {
                    items = MediaLibrary.GetMediaCollectionItems(collection.Id)
                                            .Where(item =>
                                            {
                                                var result = Path.GetFileNameWithoutExtension(item.Path) == rootName;
                                                result &= videoExtensions.Contains(Path.GetExtension(item.Name));
                                                if (!result)
                                                    MediaLibrary.Release(item, true);
                                                return result;
                                            })
                                            .ToArray();
                }
                finally
                {
                    MediaLibrary.Release(collection, true);
                }
            var result = false;
            try
            {
                foreach (var item in items)
                    result = ClassifyVideo(item) || result;
            }
            finally
            {
                MediaLibrary.Release(items, true);
            }
            return result;
        }

        private bool ClassifyInfoFile(MediaItem mediaItem)
        {
            return ClassifyGeneralFile(mediaItem);
        }

        private bool ClassifyPictureFile(MediaItem mediaItem)
        {
            return ClassifyGeneralFile(mediaItem);
        }

        

        public override bool UpdatePictures(MediaItem mediaItem)
        {
            var collection = MediaLibrary.GetMediaCollection(mediaItem.ParentCollectionId);
            try
            {
                var mediaSource = MediaLibrary.GetSource(collection.SourceId);
                try
                {
                    var reader = CreateReader(mediaSource);
                    var sourceFileInfo = reader.ReadFile(mediaItem);
                    if (sourceFileInfo is null)
                        return true;

                    var movie = MediaLibrary.GetMovieByMediaItem(mediaItem.Id);
                    var episode = MediaLibrary.GetTVShowEpisodeByMediaItem(mediaItem.Id);
                    if (movie is not null)
                    {
                        try
                        {
                            UpdateMoviePictures(movie, mediaItem, collection, mediaSource);
                        }
                        finally
                        {
                            MediaLibrary.Release(movie, true);
                        }
                    }
                    else if (episode is not null)
                    {
                        try
                        {
                            UpdateEpisodePictures(episode, mediaItem, collection, mediaSource);

                            var season = MediaLibrary.GetTVShowSeason(episode.SeasonId);
                            if (season is not null)
                            {
                                try
                                {
                                    UpdateTVShowSeasonPictures(season, collection, mediaSource);
                                }
                                finally { MediaLibrary.Release(season, true); }

                                var show = MediaLibrary.GetTVShow(season.ShowId);
                                if (show is not null)
                                    try
                                    {
                                        UpdateTVShowPictures(show, collection, mediaSource);
                                    }
                                    finally { MediaLibrary.Release(show, true); }
                            }
                        }
                        finally
                        {
                            MediaLibrary.Release(episode, true);
                        }
                    }
                }
                finally
                {
                    MediaLibrary.Release(mediaSource, true);
                }
            }
            finally
            {
                MediaLibrary.Release(collection, true);
            }
            return true;
        }
        public override bool UpdatePictures(Actor actor)
        {
            var actorFileName = actor.Name
                .ToLower()
                .Replace(" ", "_");
            var collections = MediaLibrary
                .GetActorsRoles(actor.Id)
                .Select(role =>
                {
                    MediaLibrary.Release(role, true);
                    return role;
                })
                .Select(role => role.EntryId)
                .Distinct()
                .Select(id => MediaLibrary.GetClassifiedEntry(id))
                .Where(entry => entry is not null)
                .Select(entry =>
                {
                    MediaLibrary.Release(entry, true);
                    return entry;
                })
                .OfType<IMediaItemCollectionEntry>()
                .SelectMany(entry => entry.MediaItemIds)
                .Distinct()
                .Select(id => MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .Select(mi =>
                {
                    MediaLibrary.Release(mi, true);
                    return mi;
                })
                .Select(mio => mio.ParentCollectionId)
                .Distinct()
                .Select(id => MediaLibrary.GetMediaCollection(id))
                .Where(col => col is not null)
                .Select(col =>
                {
                    MediaLibrary.Release(col);
                    return col;
                })
                .ToList();
            var mediaSources = collections
                .Select(col => col.SourceId)
                .Distinct()
                .Select(id => MediaLibrary.GetSource(id))
                .Where(source => source is not null)
                .Select(source =>
                {
                    MediaLibrary.Release(source);
                    return source;
                })
                .ToList();

            foreach (var mediaSource in mediaSources)
            {
                var pictureItems = collections
                    .Where(col => col.SourceId == mediaSource.Id)
                    .SelectMany(col => MediaLibrary.GetChildMediaCollections(col.Id))
                    .Where(col => col is not null)
                    .Select(col =>
                    {
                        MediaLibrary.Release(col, true);
                        return col;
                    })
                    .Where(col => col.Name == ".actors")
                    .SelectMany(col => MediaLibrary.GetMediaCollectionItems(col.Id))
                    .Where(mi => mi is not null)
                    .Where(mi =>
                    {
                        var result = mi.CopyType == MediaItemCopyType.Original;
                        result &= Path.GetFileNameWithoutExtension(mi.Name).ToLower() == actorFileName;
                        result &= pictureExtensions.Contains(Path.GetExtension(mi.Name));
                        MediaLibrary.Release(mi, true);
                        return result;
                    })
                    .ToList();

                foreach (var picture in pictureItems)
                    try
                    {
                        actor.PicturePath = PreparePicture(mediaSource, null, pictureItems.ToArray(), picture, actor.PicturePath, actor.PictureBackgroundColor, 240, false, out string backgroundColor);
                        actor.PictureBackgroundColor = backgroundColor;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                MediaLibrary.AddOrUpdateActor(actor);
                break;
            }
            return true;
        }

        public override async Task DeleteOrpahnedPictures(Action callback)
        {
            var cacheFolder = PathTools.Combine(FileSystem.Current.AppDataDirectory, "Cache");
            var pictureFiles = Directory.GetFiles(cacheFolder)
                .Where(file => pictureExtensions.Contains(Path.GetExtension(file).ToLower()))
                .Select(file => new FileInfo(file))
                .OrderBy(file => file.Name)
                .ToList();
            var storedFilePaths = MediaLibrary.GetClassifiedEntryPictureFileNames()
                .Concat(MediaLibrary.GetActorPictureFileNames())
                .Distinct()
                .ToList();
            foreach (var file in pictureFiles)
                try
                {
                    await Task.Delay(10);
                    var mediaItem = storedFilePaths.Where(path => path.EndsWith(file.Name));
                    if (mediaItem.Any())
                        continue;
                    MediaLibrary.AddProtocol(new DummyClassifiedEntry() { Name = file.Name, Id = 0 }, $"Delete cached file: {file.Name}");
                    file.Delete();
                }
                finally
                {
                    callback();
                }            
        }

        public override async Task RecaptureInvalidPictures(Action value)
        {
            await RecaptureInvalidPicturesAsync(EntryType.Movie, value);
            await RecaptureInvalidPicturesAsync(EntryType.TVShowEpisode, value);
            await RecaptureInvalidActorPicturesAsync(value);
            await RecaptureRoleCount(value);
        }

        private async Task RecaptureRoleCount(Action value)
        {
            var actors = MediaLibrary.GetRolesWithoutRoleCount()
                .Select(role => { MediaLibrary.Release(role); return role.ActorId; })
                .Select(id => MediaLibrary.GetActor(id))
                .Where(actor => actor is not null)
                .ToArray();
            foreach (var actor in actors)
                try
                {
                    actor.RoleCountUpdated = false;
                    MediaLibrary.AddOrUpdateActor(actor);
                    MediaLibrary.Release(actor);
                    await Task.Delay(10);
                }
                finally
                {
                    value();
                }
        }

        private async Task RecaptureInvalidActorPicturesAsync(Action value)
        {
            int offset = 0;
            int count = 1000;
            var entries = MediaLibrary.GetActorOverview(offset, count).ToArray();
            while (entries.Any())
                try
                {
                    await Task.Delay(10);
                    foreach (var entry in entries)
                    {
                        offset += 1;
                        RecaptureInvalidPictures(entry);
                        MediaLibrary.Release(entry, true);
                    }
                    entries = MediaLibrary.GetActorOverview(offset, count).ToArray();
                }
                finally
                {
                    value();
                }
        }

        private void RecaptureInvalidPictures(Actor entry)
        {
            var pEntry = entry as IPicturedEntry;
            if (pEntry is null) return;
            if (MustBeRecaptured(pEntry))
            {
                entry.NeedsPictureUpdate = true;
                MediaLibrary.AddOrUpdateActor(entry);
            }
        }

        private async Task RecaptureInvalidPicturesAsync(EntryType entryType, Action value)
        {
            int offset = 0;
            int count = 10;
            var entries = MediaLibrary.GetOverview(offset, count, "", entryType).ToArray();
            while (entries.Any())
                try
                {
                    await Task.Delay(10);
                    foreach (var entry in entries)
                    {
                        offset += 1;
                        RecaptureInvalidPictures(entry);
                        MediaLibrary.Release(entry, true);
                    }
                    entries = MediaLibrary.GetOverview(offset, count, "", entryType).ToArray();
                }
                finally
                {
                    value();
                }
        }

        private void RecaptureInvalidPictures(ClassifiedEntry entry)
        {
            var pEntry = entry as IPicturedEntry;
            if (pEntry is null) return;
            var micEntry = entry as IMediaItemCollectionEntry;
            if (micEntry is null) return;

            if (!MustBeRecaptured(pEntry))
                return;
            var mediaItems = micEntry.MediaItemIds
                .Select(id => MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .ToArray();
            foreach (var mediaItem in mediaItems)
            {
                mediaItem.NeedsPictureUpdate = true;
                MediaLibrary.AddOrUpdateMediaItem(mediaItem);
                MediaLibrary.Release(mediaItem);
            }
        }

        private bool MustBeRecaptured(IPicturedEntry pEntry)
        {
            if (!string.IsNullOrWhiteSpace(pEntry.PicturePath))
            {
                var path = PathTools.Combine(FileSystem.Current.AppDataDirectory, pEntry.PicturePath);
                if (!File.Exists(path))
                    return true;
            }
            if (!string.IsNullOrWhiteSpace(pEntry.BannerPath))
            {
                var path = PathTools.Combine(FileSystem.Current.AppDataDirectory, pEntry.BannerPath);
                if (!File.Exists(path))
                    return true;
            }
            return false;
        }
    }

}
