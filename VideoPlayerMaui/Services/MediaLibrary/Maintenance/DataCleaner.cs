using System;
using System.Diagnostics;
using System.Linq;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.Sources;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Services.MediaLibrary.Scanner;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.Services.MediaLibrary.Maintenance
{
    public class DataCleaner: IDataCleaner
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScanner _LibraryScanner;
        private readonly IStatusPublisher _StatusPublisher;

        public DataCleaner(IMediaLibrary mediaLibrary, ILibraryScanner libraryScanner, IStatusPublisher statusPublisher)
        {
            _StatusPublisher = statusPublisher;
            _LibraryScanner = libraryScanner;
            _MediaLibrary = mediaLibrary;
        }

        public DataCleaningMode Mode { get; set; }

        private bool clearing = false;

        public async Task RunAsync()
        {
            if (clearing)
                return;
            clearing = true;
            try
            {
                _LibraryScanner.Stop();
                _StatusPublisher.AddStatus($"Warte auf laufende Hintergrundaktivitäten.", true);
                await _LibraryScanner.WaitForFinish();
                await RunAsync(5);
            }
            finally
            {
                clearing = false;
                _StatusPublisher.AddStatus(string.Empty, false);
                _LibraryScanner.Start();
            }
        }

        private async Task RunAsync(int tryCounter)
        {
            bool repeat = false;
            try
            {
                switch (Mode)
                {
                    case DataCleaningMode.Complete:
                        await RunCompleteAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                repeat = tryCounter > 0;
                Debug.WriteLine(ex);
                _StatusPublisher.AddStatus($"{ex.Message}", true);
            }
            finally { }
            if (repeat)
                await RunAsync(tryCounter - 1);
        }

        private async Task RunCompleteAsync()
        {
            _StatusPublisher.AddStatus($"Lade Quellen", false);
            var sources = await _MediaLibrary.GetSourcesAsync();
            foreach (var  source  in sources)
            {
                await CleanSourceAsync(source);
            }
        }

        private async Task CleanSourceAsync(MediaSource source)
        {
            _StatusPublisher.AddStatus($"Bereinige {source.Name}", false);
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

            source = await _MediaLibrary.GetSourceAsync(source.Id);
            source.LastScan = DateTime.MinValue;
            await _MediaLibrary.AddSourceAsync(source);
        }

        private async Task RemoveMediaItemCollection(MediaItemCollection collection)
        {
            _StatusPublisher.AddStatus($"{collection.Name}", false);
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
            _StatusPublisher.AddStatus($"{mediaItem.Name}", false);
            await _MediaLibrary.RemoveMediaItemAsync(mediaItem);
        }

        private async Task RemoveMovie(Movie movie)
        {
            _StatusPublisher.AddStatus($"{movie.Name}", false);
            await _MediaLibrary.RemoveMovieAsync(movie);
        }

        private async Task RemoveTVShow(TVShow show)
        {
            _StatusPublisher.AddStatus($"{show.Name}", false);
            await _MediaLibrary.RemoveTVShowAsync(show);
        }

    }
}
