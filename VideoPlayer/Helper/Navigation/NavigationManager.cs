using CommunityToolkit.Maui.Views;
using System;
using System.Linq;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Views.MediaLists;
using VideoPlayer.Views.VideoPlayer;

namespace VideoPlayer.Helper.Navigation
{
    internal class NavigationManager: INavigationManager
    {

        private readonly MediaLibrarySettings _Settings;
        private readonly IMediaLibrary _MediaLibrary;
        private readonly IMediaDownloader _MediaDownloader;

        public NavigationManager(
            MediaLibrarySettings settings,
            IMediaLibrary mediaLibrary,
            IMediaDownloader mediaDownloader)
        {
            _MediaDownloader = mediaDownloader;
            _Settings = settings;
            _MediaLibrary = mediaLibrary;
            Routing.RegisterRoute("movies", typeof(MoviesPage));
            Routing.RegisterRoute("tvshows", typeof(TVShowsPage));
            Routing.RegisterRoute("player", typeof(VideoPlayerPage));
        }

        public void OpenMovies()
        {
            NavigateToRoute("movies");
        }

        public void OpenTVShows()
        {
            NavigateToRoute("tvshows");
        }

        protected void NavigateToRoute(string route, Dictionary<string, object> args = null)
        {
            if (args == null)
                Shell.Current.GoToAsync(route);
            else
                Shell.Current.GoToAsync(route, args);
        }

        public void NavigateBack()
        {
            MainThread.InvokeOnMainThreadAsync(() => { Shell.Current.Navigation.RemovePage(Shell.Current.CurrentPage); });
        }

        public void OpenTVShow(TVShow show)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Show", show }
            };
            NavigateToRoute($"tvshows", navigationParameter);
        }

        public void OpenTVShowSeason(TVShowSeason tVShowSeason)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Season", tVShowSeason }
            };
            NavigateToRoute($"tvshows", navigationParameter);
        }

        public void OpenTVShowEpisode(TVShowEpisode tVShowEpisode)
        {
            throw new NotImplementedException();
        }

        public void OpenMovieCollection(MovieCollection movieCollection)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Collection", movieCollection }
            };
            NavigateToRoute($"movies", navigationParameter);
        }

        public async Task OpenMovie(Movie movie)
        {
            DownloadSource source = new DownloadSource();

            OpenLoading(source);

            MediaItem item = null;
            foreach (var mediaItemId in movie.MediaItems)
            {
                item = await _MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (item != null)
                    break;
            }
            if (item == null)
                return;

            item = await _MediaDownloader.CacheAsync(item);
            var mediaSource = MediaSource.FromFile(item.Path);
            source.SetMediaSource(item, mediaSource);
        }

        private string loadingMoviePath = string.Empty;

        private void OpenLoading(DownloadSource downloadSource)
        {
            if (string.IsNullOrWhiteSpace(loadingMoviePath))
                loadingMoviePath = findLocalFile("loading.mp4");
            var navigationParameter = new Dictionary<string, object>
            {
                { "VideoSource", File.Exists(loadingMoviePath) ? loadingMoviePath : string.Empty },
                { "DownloadSource", downloadSource }
            };
            NavigateToRoute($"player", navigationParameter);
        }

        private string findLocalFile(string fileName, DirectoryInfo folder = null)
        {
            DirectoryInfo tempFolder = new DirectoryInfo(FileSystem.Current.CacheDirectory);
            FileInfo tempFile = new FileInfo(Path.Combine(tempFolder.FullName, fileName));
            if (tempFile.Exists)
                tempFile.Delete();

            if (folder == null)
                folder = new DirectoryInfo(_Settings.RessourcePath);
            try
            {
                FileInfo file = new FileInfo(Path.Combine(folder.FullName, fileName));
                if (file.Exists)
                    return file.FullName;

                try
                {
                    file = folder.GetFiles($"*{fileName}").FirstOrDefault();
                    if ((file != null) && file.Exists)
                        return file.FullName;
                }
                catch { }

                foreach (var subDir in folder.GetDirectories())
                {
                    var path = findLocalFile(fileName, subDir);
                    if (!string.IsNullOrWhiteSpace(path))
                        return path;
                }
            }
            catch { }
            return string.Empty;
        }

    }
}
