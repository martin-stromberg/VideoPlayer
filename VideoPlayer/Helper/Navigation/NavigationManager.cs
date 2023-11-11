using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Views.Categorization;
using VideoPlayer.Views.MediaLists;
using VideoPlayer.Views.VideoPlayer;

namespace VideoPlayer.Helper.Navigation
{
    internal class NavigationManager: INavigationManager
    {

        private readonly MediaLibraryEnvironment _Settings;
        private readonly IMediaLibrary _MediaLibrary;
        private readonly IMediaDownloader _MediaDownloader;
        private readonly IPlaylistManager _PlaylistManager;

        public NavigationManager(
            MediaLibraryEnvironment settings,
            IMediaLibrary mediaLibrary,
            IMediaDownloader mediaDownloader,
            IPlaylistManager playlistManager)
        {
            _PlaylistManager = playlistManager;
            _MediaDownloader = mediaDownloader;
            _Settings = settings;
            _MediaLibrary = mediaLibrary;
            Routing.RegisterRoute("movies", typeof(MoviesPage));
            Routing.RegisterRoute("tvshows", typeof(TVShowsPage));
            Routing.RegisterRoute("player", typeof(VideoPlayerPage));
            Routing.RegisterRoute("mediaitems", typeof(MediaList));
            Routing.RegisterRoute("mediaitem", typeof(MediaItemCardPage));
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

        public async Task OpenTVShowEpisodeAsync(TVShowEpisode tVShowEpisode, Func<IEnumerable<BaseModel>> GetCollectionElements)
        {
            await _PlaylistManager.StartTVShowPlaylistAsync(tVShowEpisode, GetCollectionElements);
            NavigateToRoute($"player");
        }

        public async Task OpenMediaItemAsync(MediaItem mediaItem)
        {
            await _PlaylistManager.StartMediaItemPlaylistAsync(mediaItem);
            NavigateToRoute($"player");
        }

        public void OpenMovieCollection(MovieCollection movieCollection)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Collection", movieCollection }
            };
            NavigateToRoute($"movies", navigationParameter);
        }

        public async Task OpenMovie(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements)
        {
            await _PlaylistManager.StartMoviePlaylistAsync(movie, GetCollectionElements);
            NavigateToRoute($"player");
        }

        public async Task OpenPlaylistPlaybackAsync()
        {
            await _PlaylistManager.StartPlaybackAsync();
            NavigateToRoute($"player");
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

        public void OpenUncategrized()
        {
            NavigateToRoute($"mediaitems");
        }

        public async Task OpenMediaItemDetailsAsync(MediaItem mediaItem)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Item", mediaItem }
            };
            NavigateToRoute("mediaitem", navigationParameter);
            await Task.CompletedTask;
        }

    }
}
