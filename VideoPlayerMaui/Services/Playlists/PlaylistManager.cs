using CommunityToolkit.Maui.Views;
using System;
using System.Linq;
using VideoPlayer.Models;
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

        private readonly MediaLibraryEnvironment _Settings;
        private readonly IMediaLibrary _MediaLibrary;
        private readonly IMediaDownloader _MediaDownloader;

        public PlaylistManager(
            IMediaLibrary mediaLibrary,
            IMediaDownloader mediaDownloader,
            MediaLibraryEnvironment settings)
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

        public async Task StartTVShowPlaylistAsync(TVShowEpisode tVShowEpisode, Func<IEnumerable<BaseModel>> GetCollectionElements)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            GeneralPlaylist.Items.Clear();
            await AddTVShowEpisode(tVShowEpisode, currentPlaylistCompletionSessionId, () => { });
            await SavePlaylistAsync(GeneralPlaylist);
        }

        public async Task StartTVShowPlaylistAsync(TVShow show)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            GeneralPlaylist.Items.Clear();
            Task SaveTask = null;
            bool AsyncSave = false;
            await AddTVShowEpisodes(show,
                                    true,
                                    currentPlaylistCompletionSessionId,
                                    async () =>
                                    {
                                        SaveTask = SavePlaylistAsync(GeneralPlaylist);
                                        if (AsyncSave)
                                            await SaveTask;
                                    });
            if (SaveTask != null)
                await SaveTask;
            AsyncSave = true;
        }

        public async Task StartTVShowPlaylistAsync(TVShowSeason season)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            GeneralPlaylist.Items.Clear();
            Task SaveTask = null;
            bool AsyncSave = false;
            await AddTVShowSeasonEpisodes(season,
                                          true,
                                          currentPlaylistCompletionSessionId,
                                          async () =>
                                          {
                                              SaveTask = SavePlaylistAsync(GeneralPlaylist);
                                              if (AsyncSave)
                                                  await SaveTask;
                                          });
            if (SaveTask != null)
                await SaveTask;
            AsyncSave = true;
        }

        private async Task AddTVShowEpisodes(TVShow show, bool startPlayback, int session, Action Finished)
        {
            bool started = startPlayback;
            bool isFirst = true;
            var seasons = await _MediaLibrary.GetTVShowSeasons(show.Id);
            var count = seasons.Count();
            foreach (var season in seasons)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                var task = AddTVShowSeasonEpisodes(season,
                                                   startPlayback && isFirst,
                                                   session,
                                                   () =>
                                                   {
                                                       count -= 1;
                                                       if (count == 0)
                                                           Finished();
                                                   });
                if (isFirst || !started)
                    await task;
                isFirst = false;
            }
            if (isFirst)
                Finished();
        }

        private async Task AddTVShowSeasonEpisodes(
            TVShowSeason season,
            bool startPlayback,
            int session,
            Action Finished)
        {
            bool started = startPlayback;
            bool isFirst = true;
            var episodes = await _MediaLibrary.GetTVShowEpisodes(season.Id);
            var count = episodes.Count();
            foreach (var episode in episodes)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                var task = AddTVShowEpisode(episode,
                                            session,
                                            () =>
                                            {
                                                count -= 1;
                                                if (count == 0)
                                                    Finished();
                                            });
                if (isFirst || !started)
                    await task;
                isFirst = false;
            }
            if (isFirst)
                Finished();
        }

        private async Task AddTVShowEpisode(TVShowEpisode episode, int session, Action Finished)
        {
            if (session != currentPlaylistCompletionSessionId)
                return;
            var mediaItem = await GetFirstMediaItem(episode.MediaItems);
            GeneralPlaylist.Add(mediaItem);
            Finished();
        }

        public async Task StartMoviePlaylistAsync(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            GeneralPlaylist.Items.Clear();
            await AddMovie(movie, currentPlaylistCompletionSessionId, () => { });
            await SavePlaylistAsync(GeneralPlaylist);
        }

        public async Task StartMoviePlaylistAsync(MovieCollection movieCollection)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            GeneralPlaylist.Items.Clear();
            Task SaveTask = null;
            bool AsyncSave = false;
            await AddMovieCollection(movieCollection,
                                     true,
                                     currentPlaylistCompletionSessionId,
                                     async () =>
                                     {
                                         SaveTask = SavePlaylistAsync(GeneralPlaylist);
                                         if (AsyncSave)
                                             await SaveTask;
                                     });
            if (SaveTask != null)
                await SaveTask;
            AsyncSave = true;
        }

        private async Task AddMovieCollection(
            MovieCollection movieCollection,
            bool startPlayback,
            int session,
            Action Finished)
        {
            var started = startPlayback;
            var isFirst = true;
            var movies = await _MediaLibrary.GetMovies(movieCollection.Id);
            var count = movies.Count();
            foreach (var movie in movies)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                var task = AddMovie(movie,
                                    session,
                                    () =>
                                    {
                                        count -= 1;
                                        if (count == 0)
                                            Finished();
                                    });
                if (isFirst || !started)
                    await task;
                isFirst = false;
            }
            if (isFirst)
                Finished();
        }

        private async Task AddMovie(Movie movie, int session, Action Finished)
        {
            if (session != currentPlaylistCompletionSessionId)
                return;
            var mediaItem = await GetFirstMediaItem(movie.MediaItems);
            GeneralPlaylist.Add(mediaItem);
            Finished();
        }

        public Task StartPlaybackAsync()
        {
            return Task.CompletedTask;
        }

        private int currentPlaylistCompletionSessionId = 0;

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
            source.SetMediaSource(null, null, MediaSource.FromFile(loadingMoviePath));

            Task.Run(async () =>
            {
                var typedItem = await _MediaLibrary.GetTypedItem(item.Item.Id);
                var mediaItem = await _MediaDownloader.CacheAsync(item.Item);
                var mediaSource = MediaSource.FromFile(mediaItem.Path);
                source.SetMediaSource(mediaItem, typedItem, mediaSource);
            });

            return source;
        }

        public DownloadSource ProcessMediaEnded(MediaItem item)
        {
            GeneralPlaylist.RemoveUpTo(item);
            if (item.CopyType == MediaItemCopyType.Cache)
                _ = _MediaLibrary.RemoveMediaItemAsync(item);
            _ = SavePlaylistAsync(GeneralPlaylist);
            return GetFirstVideoSource();
        }

        public Playlist GeneralPlaylist { get; private set; }

        private async Task SavePlaylistAsync(Playlist playlist)
        {
            await _MediaLibrary.AddPlaylistAsync(playlist);
        }

    }
}
