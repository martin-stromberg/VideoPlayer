using CommunityToolkit.Maui.Views;
using System;
using System.Linq;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.Playlists;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;

namespace VideoPlayer.Services.Playlists
{
    public class PlaylistManager: IPlaylistManager
    {

        private readonly MediaLibrarySettings _Settings;
        private readonly IMediaLibrary _MediaLibrary;
        private readonly IMediaDownloader _MediaDownloader;

        public PlaylistManager(
            IMediaLibrary mediaLibrary,
            IMediaDownloader mediaDownloader,
            MediaLibrarySettings settings)
        {
            _Settings = settings;
            _MediaDownloader = mediaDownloader;
            _MediaLibrary = mediaLibrary;
        }

        public async Task InitializeAsync()
        {
            await InitializeGeneralPlaylist();
        }

        private async Task InitializeGeneralPlaylist()
        {
            var playlist = (await _MediaLibrary.GetPlaylists(PlaylistType.General)).FirstOrDefault();
            if (playlist == null)
            {
                playlist = new Playlist() { Type = PlaylistType.General, Name = $"Allgemein" };
                await _MediaLibrary.AddPlaylistAsync(playlist);
            }
            GeneralPlaylist = playlist;
        }

        private async Task<MediaItem> GetFirstMediaItem(long[] mediaItems)
        {
            MediaItem item = null;
            foreach (var mediaItemId in mediaItems)
            {
                item = await _MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (item == null)
                    continue;
                if (item.CopyType != MediaItemCopyType.None)
                    continue;
                break;
            }
            return item;
        }

        public async Task StartTVShowPlaybackAsync(TVShowEpisode tVShowEpisode)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            var mediaItem = await GetFirstMediaItem(tVShowEpisode.MediaItems);
            GeneralPlaylist.Items.Clear();
            GeneralPlaylist.Items.Add(mediaItem);
            AddNextTVShowEpisodes(tVShowEpisode, currentPlaylistCompletionSessionId);
        }

        private async void AddNextTVShowEpisodes(TVShowEpisode episode, int sessionId)
        {
            var season = await _MediaLibrary.GetTVShowSeason(episode.SeasonId);
            var episodes = await _MediaLibrary.GetTVShowEpisodes(season.Id);
            foreach (var item in episodes
                .OrderBy(item => item.EpisodeNo)
                .SkipWhile(item => item.Id != episode.Id)
                .SkipWhile(item => item.Id == episode.Id))
            {
                var mI = await GetFirstMediaItem(item.MediaItems);
                if (sessionId != currentPlaylistCompletionSessionId)
                    return;
                GeneralPlaylist.Add(mI);
            }

            var show = await _MediaLibrary.GetTVShow(season.ShowId);
            var seasons = await _MediaLibrary.GetTVShowSeasons(show.Id);
            foreach (var item in seasons
                .OrderBy(item => item.Name)
                .SkipWhile(item => item.Id != episode.SeasonId)
                .SkipWhile(item => item.Id == episode.SeasonId))
            {
                if (sessionId != currentPlaylistCompletionSessionId)
                    return;
                episodes = await _MediaLibrary.GetTVShowEpisodes(item.Id);
                foreach (var nextSeason in episodes)
                {
                    var mI = await GetFirstMediaItem(nextSeason.MediaItems);
                    if (sessionId != currentPlaylistCompletionSessionId)
                        return;
                    GeneralPlaylist.Add(mI);
                }
            }
        }

        public async Task StartMoviePlaybackAsync(Movie movie)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            var mediaItem = await GetFirstMediaItem(movie.MediaItems);
            GeneralPlaylist.Items.Clear();
            GeneralPlaylist.Items.Add(mediaItem);

            AddNextCollectionMovies(movie, currentPlaylistCompletionSessionId);
        }

        private int currentPlaylistCompletionSessionId = 0;

        private async void AddNextCollectionMovies(Movie movie, int sessionId)
        {
            var collection = await _MediaLibrary.GetMovieCollection(movie);
            if (collection == null)
                return;
            if (sessionId != currentPlaylistCompletionSessionId)
                return;
            var movies = await _MediaLibrary.GetMovies(collection.Id);
            if (sessionId != currentPlaylistCompletionSessionId)
                return;
            foreach (var mov in movies
                .OrderBy(mov => mov.Name)
                .SkipWhile(mov => mov.Id != movie.Id)
                .SkipWhile(mov => mov.Id == movie.Id))
            {
                var mI = await GetFirstMediaItem(mov.MediaItems);
                if (sessionId != currentPlaylistCompletionSessionId)
                    return;
                GeneralPlaylist.Add(mI);
            }
        }

        private string loadingMoviePath = string.Empty;

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

        public DownloadSource GetFirstVideoSource()
        {
            var item = GeneralPlaylist.Items.FirstOrDefault();
            if (item == null)
                return null;

            if (string.IsNullOrWhiteSpace(loadingMoviePath))
                loadingMoviePath = findLocalFile("loading.mp4");
            DownloadSource source = new DownloadSource();
            source.SetMediaSource(null, MediaSource.FromFile(loadingMoviePath));

            Task.Run(async () =>
            {
                item = await _MediaDownloader.CacheAsync(item);
                var mediaSource = MediaSource.FromFile(item.Path);
                source.SetMediaSource(item, mediaSource);
            });

            return source;
        }

        public DownloadSource ProcessMediaEnded(MediaItem item)
        {
            GeneralPlaylist.RemoveUpTo(item);
            if (item.CopyType == MediaItemCopyType.Cache)
                _ = _MediaLibrary.RemoveMediaItemAsync(item);
            return GetFirstVideoSource();
        }

        public Playlist GeneralPlaylist { get; private set; }

    }
}
