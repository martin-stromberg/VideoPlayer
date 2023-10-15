using System;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.Helper.LibraryScan
{
    public interface ILibraryCleanup
    {

        Task RunAsync();

    }

    public class LibraryCleanup: ILibraryCleanup
    {

        private readonly IMediaLibrary _MediaLibrary;

        public LibraryCleanup(IMediaLibrary mediaLibrary)
        {
            _MediaLibrary = mediaLibrary;
        }

        public async Task RunAsync()
        {
            await FindOrphanedMediaItemCollections();
            await FindOrphanedMediaItems();
            await FindOrphanedTVShowEpsisodeMediaItemAssignmentsAsync();
            await FindOrpahnedMovieMediaItemAssignmentsAsync();
        }

        private async Task FindOrphanedMediaItemCollections()
        {
            var collections = await _MediaLibrary.GetAllMediaItemCollectionsAsync();
            foreach (var collection in collections.OrderBy(coll => coll.ParentCollectionId))
                await FindOrphanedMediaItemCollections(collection);
        }

        private async Task FindOrphanedMediaItemCollections(MediaItemCollection collection)
        {
            if (collection.ParentCollectionId != 0)
            {
                var parentCollection = await _MediaLibrary.GetMediaItemCollectionAsync(collection.ParentCollectionId);
                if (parentCollection == null)
                {
                    await _MediaLibrary.RemoveMediaItemCollection(collection);
                    return;
                }
            }

            var source = await _MediaLibrary.GetSourceAsync(collection.MediaSourceId);
            if (source == null)
            {
                await _MediaLibrary.RemoveMediaItemCollection(collection);
                return;
            }
        }

        private async Task FindOrphanedMediaItems()
        {
            var mediaItems = await _MediaLibrary.GetAllMediaItems();
            foreach (var mediaItem in mediaItems)
                await FindOrphanedMediaItemsAsync(mediaItem);
        }

        private async Task FindOrphanedMediaItemsAsync(MediaItem mediaItem)
        {
            var collection = await _MediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId);
            if (collection != null)
                return;
            await _MediaLibrary.RemoveMediaItemAsync(mediaItem);
        }

        private async Task FindOrpahnedMovieMediaItemAssignmentsAsync()
        {
            var movies = await _MediaLibrary.GetMovies();
            foreach (var movie in movies)
                await FindOrpahnedMovieMediaItemAssignmentsAsync(movie);
        }

        private async Task FindOrpahnedMovieMediaItemAssignmentsAsync(Movie movie)
        {
            List<long> correctIds = new List<long>();
            foreach (var mediaItemId in movie.MediaItems.OrderBy(id => id))
            {
                var mediaItem = await _MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (mediaItem != null)
                    correctIds.Add(mediaItem.Id);
            }
            var newArray = correctIds.ToArray();
            if (movie.MediaItems.SequenceEqual(newArray))
                return;
            movie.MediaItems = newArray;
            await _MediaLibrary.AddMovieAsync(movie);
        }

        private async Task FindOrphanedTVShowEpsisodeMediaItemAssignmentsAsync()
        {
            var shows = await _MediaLibrary.GetTVShows();
            foreach (var show in shows)
                await FindOrphanedTVShowEpsisodeMediaItemAssignmentsAsync(show);
        }

        private async Task FindOrphanedTVShowEpsisodeMediaItemAssignmentsAsync(TVShow show)
        {
            var seasons = await _MediaLibrary.GetTVShowSeasons(show.Id);
            foreach (var season in seasons)
                await FindOrphanedTVShowEpsisodeMediaItemAssignmentsAsync(show, season);
        }

        private async Task FindOrphanedTVShowEpsisodeMediaItemAssignmentsAsync(TVShow show, TVShowSeason season)
        {
            var episodes = await _MediaLibrary.GetTVShowEpisodes(season.Id);
            foreach (var episode in episodes)
                await FindOrphanedTVShowEpsisodeMediaItemAssignmentsAsync(show, season, episode);
        }

        private async Task FindOrphanedTVShowEpsisodeMediaItemAssignmentsAsync(
            TVShow show,
            TVShowSeason season,
            TVShowEpisode episode)
        {
            List<long> correctIds = new List<long>();
            foreach (var mediaItemId in episode.MediaItems.OrderBy(id => id))
            {
                var mediaItem = await _MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (mediaItem != null)
                    correctIds.Add(mediaItem.Id);
            }
            var newArray = correctIds.ToArray();
            if (episode.MediaItems.SequenceEqual(newArray))
                return;
            episode.MediaItems = newArray;
            await _MediaLibrary.AddTVShowEpisodeAsync(show, season, episode);
        }

    }
}
