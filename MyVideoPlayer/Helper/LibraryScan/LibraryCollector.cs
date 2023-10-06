using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;
using VideoPlayerLib.Services.MediaLibrary.Models.Meta;

namespace MyVideoPlayer.Helper.LibraryScan
{
    public interface ILibraryCollector
    {

    }
    public class LibraryCollector : ILibraryCollector
    {
        private readonly IMediaLibrary mediaLibrary;

        public LibraryCollector(IMediaLibrary mediaLibrary)
            :base()
        {
            this.mediaLibrary = mediaLibrary;
            this.mediaLibrary.ModelElementAdded += MediaLibrary_ModelElementAddedAsync;
            this.mediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdatedAsync;
        }

        private void MediaLibrary_ModelElementUpdatedAsync(object sender, VideoPlayerLib.Services.MediaLibrary.Models.BaseModelEventArgs e)
        {
            CollectMediaItemAsync(e.Element as MediaItem).Wait();
        }
        private void MediaLibrary_ModelElementAddedAsync(object sender, VideoPlayerLib.Services.MediaLibrary.Models.BaseModelEventArgs e)
        {
            var mediaItem = e.Element as MediaItem;
            if (mediaItem == null) return;
            if (mediaItem.CopyType != MediaItemCopyType.None) return;
            CollectMediaItemAsync(e.Element as MediaItem).Wait();
        }
        private async Task CollectMediaItemAsync(MediaItem mediaItem)
        {
            if (mediaItem == null)
                return;
            if (mediaItem.MetaInfo is MovieInformation)
                await CollectMovieAsync(mediaItem, mediaItem.MetaInfo as MovieInformation);
            else if (mediaItem.MetaInfo is EpisodeInformation)
                await CollectTVShowAsync(mediaItem, mediaItem.MetaInfo as EpisodeInformation);
        }

        private async Task CollectTVShowAsync(MediaItem mediaItem, EpisodeInformation episodeInformation)
        {
            TVShowInformation showInformation = null;
            MediaItemCollection showCollection = null;
            MediaItemCollection seasonCollection = null;
            var collection = await mediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId);
            while (showInformation == null && collection != null)
            {
                showInformation = collection.MetaInfo as TVShowInformation;
                if (showInformation != null)
                    showCollection = collection;

                if (showInformation != null)
                    seasonCollection = collection;
                collection = await mediaLibrary.GetMediaItemCollectionAsync(collection.ParentCollectionId);
            }

            var show = new TVShow()
            {
                Name = showInformation?.Title ?? episodeInformation.ShowName,
                PicturePath = showCollection?.PicturePath
            };
            var season = new TVShowSeason()
            {
                Name = $"{episodeInformation.Season}",
                PicturePath = seasonCollection?.PicturePath
            };
            var episode = new TVShowEpisode()
            {
                Name = mediaItem.Name,
            };
            episode.Name = episodeInformation.Title;
            episode.EpisodeNo = episodeInformation.Episode;
            episode.MediaItems = new long[] { mediaItem.Id };
            await CollectShowAsync(show, season, episode);
        }

        private async Task CollectShowAsync(TVShow show, TVShowSeason season, TVShowEpisode episode)
        {
            if (string.IsNullOrWhiteSpace(show.Name))
                return;

            var existingShows = await mediaLibrary.FindTVShowByNameAsync(show.Name);
            var existingShow = (show.Id != 0) ? await mediaLibrary.FindTVShowAsync(show.Id): existingShows.FirstOrDefault();
            if (existingShow == null) 
            {
                await mediaLibrary.AddTVShowAsync(show);
                await mediaLibrary.AddTVShowSeasonAsync(show, season);
                await mediaLibrary.AddTVShowEpisodeAsync(existingShow, season, episode);
                return;
            }
            existingShow.PicturePath = show.PicturePath ?? existingShow.PicturePath;
            await mediaLibrary.AddTVShowAsync(existingShow);

            var existingSeason = (await mediaLibrary.GetTVShowSeasons(existingShow.Id))
                .Where(s => s != null)
                .FirstOrDefault(s => s.Name == season.Name); 
            if (existingSeason == null)
            {
                await mediaLibrary.AddTVShowSeasonAsync(existingShow, season);
                return;
            }
            existingSeason.PicturePath = season.PicturePath ?? existingSeason.PicturePath;
            await mediaLibrary.AddTVShowSeasonAsync(existingShow, existingSeason);

            var existingEpisode = (await mediaLibrary.GetTVShowEpisodes(existingSeason.Id))
                .FirstOrDefault(e => e.EpisodeNo == episode.EpisodeNo); 
            if (existingEpisode == null)
            {
                await mediaLibrary.AddTVShowEpisodeAsync(existingShow, existingSeason, episode);
                return;
            }

            existingEpisode.Name = existingEpisode.Name ?? episode.Name;
            existingEpisode.MediaItems = existingEpisode
                .MediaItems
                .Concat(episode.MediaItems)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            await mediaLibrary.AddTVShowEpisodeAsync(existingShow, existingSeason, existingEpisode);
        }

        private async Task CollectMovieAsync(MediaItem mediaItem, MovieInformation movieInformation)
        {
            var movie = new Movie()
            {
                Name = mediaItem.Name
            };
            movie.Name = movieInformation.Title;
            movie.Genre = movieInformation.Genre;
            movie.Plot = movieInformation.Plot;
            movie.MediaItems = new long[] { mediaItem.Id };
            movie.PicturePath = mediaItem.PicturePath;

            var existingMovie = await mediaLibrary.FindMovieAsync(mediaItem.Id);
            if (existingMovie == null)
            {
                await mediaLibrary.AddMovieAsync(movie);
                return;
            }

            existingMovie.Genre = existingMovie.Genre ?? movieInformation.Genre;
            existingMovie.Plot = existingMovie.Plot ?? movieInformation.Plot;
            existingMovie.MediaItems = existingMovie
                .MediaItems
                .Concat(movie.MediaItems)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            movie.PicturePath = mediaItem.PicturePath;
            await mediaLibrary.AddMovieAsync(existingMovie);
        }
    }
}
