using System;
using System.Diagnostics;
using System.Linq;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.Sources;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Services.MediaLibrary.Scanner;

namespace VideoPlayer.Services.MediaLibrary.Maintenance
{
    public class DataCleaner: IDataCleaner
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScanner _LibraryScanner;

        public DataCleaner(IMediaLibrary mediaLibrary, ILibraryScanner libraryScanner)
        {
            _LibraryScanner = libraryScanner;
            _MediaLibrary = mediaLibrary;
        }

        public DataCleaningMode Mode { get; set; }

        public async Task RunAsync()
        {
            _LibraryScanner.Stop();
            try
            {
                await _LibraryScanner.WaitForFinish();
                switch (Mode)
                {
                    case DataCleaningMode.Complete:
                        await RunCompleteAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                _LibraryScanner.Start();
            }
        }

        private async Task RunCompleteAsync()
        {
            var sources = await _MediaLibrary.GetSourcesAsync();
            foreach (var  source  in sources)
            {
                await CleanSourceAsync(source);
            }
        }

        private async Task CleanSourceAsync(MediaSource source)
        {
            var collections = await _MediaLibrary.GetMediaItemCollectionsAsync(source.Id);
            foreach (var collection in collections)
            {
                await RemoveMediaItemCollection(collection);
            }

            var movies = await _MediaLibrary.GetMovies();
            foreach (var movie in movies)
            {
                await RemoveMovie(movie);
            }

            var shows = await _MediaLibrary.GetTVShows();
            foreach (var show in shows)
                await RemoveTVShow(show);
        }

        private async Task RemoveMediaItemCollection(MediaItemCollection collection)
        {
            var mediaItems = await _MediaLibrary.GetMediaItemsAsync(collection.Id);
            foreach (var mediaItem in mediaItems)
                await RemoveMediaItem(mediaItem);
            var collections = await _MediaLibrary.GetChildMediaItemCollectionsAsync(collection.Id);
            foreach (var childCollection in collections)
                await RemoveMediaItemCollection(childCollection);
            await _MediaLibrary.RemoveMediaItemCollection(collection);
        }

        private async Task RemoveMediaItem(MediaItem mediaItem)
        {
            await _MediaLibrary.RemoveMediaItemAsync(mediaItem);
        }

        private async Task RemoveMovie(Movie movie)
        {
            await _MediaLibrary.RemoveMovieAsync(movie);
        }

        private async Task RemoveTVShow(TVShow show)
        {
            await _MediaLibrary.RemoveTVShowAsync(show);
        }

    }
}
