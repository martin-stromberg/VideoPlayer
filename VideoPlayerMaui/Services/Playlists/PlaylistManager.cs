using CommunityToolkit.Maui.Views;
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
        private readonly IDownloadManager _DownloadManager;

        public PlaylistManager(
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            MediaLibraryEnvironment settings)
        {
            _Settings = settings;
            _MediaLibrary = mediaLibrary;
            _DownloadManager = downloadManager;
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

        public async Task StartTVShowPlaylistAsync(TVShowEpisode episode, Func<IEnumerable<BaseModel>> GetCollectionElements)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            GeneralPlaylist.Items.Clear();
            await AddTVShowEpisodes(episode,
                                    true,
                                    currentPlaylistCompletionSessionId,
                                    async () => { await SavePlaylistAsync(GeneralPlaylist); });
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
            var seasons = (await _MediaLibrary.GetTVShowSeasons(show.Id))
                .OrderBy(s => s.Name)
                .ToArray();
            var count = seasons.Count();
            Task previousTask = null;
            Task nextTask = null;
            foreach (var season in seasons)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                while (nextTask != null)
                    await Task.Delay(100);
                nextTask = AddTVShowSeasonEpisodes(season,
                                                   startPlayback && isFirst,
                                                   session,
                                                   async () =>
                                                   {
                                                       count -= 1;
                                                       if (count == 0)
                                                           Finished();
                                                       else if (nextTask != null)
                                                       {
                                                           var currentTask = previousTask;
                                                           previousTask = nextTask;
                                                           if (currentTask != null)
                                                               await currentTask;
                                                           nextTask = null;
                                                       }
                                                   });
                if (isFirst || !started)
                {
                    await nextTask;
                    nextTask = null;
                }
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
            var episodes = (await _MediaLibrary.GetTVShowEpisodes(season.Id))
                .OrderBy(e => e.EpisodeNo)
                .ToArray();
            var count = episodes.Count();
            Task previousTask = null;
            Task nextTask = null;
            foreach (var episode in episodes)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                while (nextTask != null)
                    await Task.Delay(100);
                nextTask = AddTVShowEpisode(episode,
                                            session,
                                            async () =>
                                            {
                                                count -= 1;
                                                if (count == 0)
                                                    Finished();
                                                else if (nextTask != null)
                                                {
                                                    var currentTask = previousTask;
                                                    previousTask = nextTask;
                                                    if (currentTask != null)
                                                        await currentTask;
                                                    nextTask = null;
                                                }
                                            });
                if (isFirst || !started)
                {
                    await nextTask;
                    nextTask = null;
                }
                isFirst = false;
            }
            if (isFirst)
                Finished();
        }

        private async Task AddTVShowEpisodes(TVShowEpisode episode, bool startPlayback, int session, Action Finished)
        {
            bool started = startPlayback;
            bool isFirst = true;
            var season = await _MediaLibrary.GetTVShowSeason(episode.SeasonId);

            var episodes = (await _MediaLibrary.GetTVShowEpisodes(season.Id))
                .OrderBy(e => e.EpisodeNo)
                .SkipWhile(e => e.EpisodeNo != episode.EpisodeNo)
                .ToArray();
            var count = episodes.Count();

            Task previousTask = null;
            Task nextTask = null;
            foreach (var currEpisode in episodes)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                while (nextTask != null)
                    await Task.Delay(100);
                nextTask = AddTVShowEpisode(currEpisode,
                                            session,
                                            async () =>
                                            {
                                                count -= 1;
                                                if (count == 0)
                                                    Finished();
                                                else if (nextTask != null)
                                                {
                                                    var currentTask = previousTask;
                                                    previousTask = nextTask;
                                                    if (currentTask != null)
                                                        await currentTask;
                                                    nextTask = null;
                                                }
                                            });
                if (isFirst || !started)
                {
                    await nextTask;
                    nextTask = null;
                }
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

        public async Task StartMediaItemPlaylistAsync(MediaItem mediaItem)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            GeneralPlaylist.Items.Clear();
            GeneralPlaylist.Add(mediaItem);
            await SavePlaylistAsync(GeneralPlaylist);
        }

        private async Task AddMovieCollection(
            MovieCollection movieCollection,
            bool startPlayback,
            int session,
            Action Finished)
        {
            var started = startPlayback;
            var isFirst = true;
            var movies = (await _MediaLibrary.GetMovies(movieCollection.Id))
                .OrderBy(m => m.Date)
                .ThenBy(m => m.Name)
                .ToArray();
            var count = movies.Count();
            Task previousTask = null;
            Task nextTask = null;
            foreach (var movie in movies)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                while (nextTask != null)
                    await Task.Delay(100);
                nextTask = AddMovie(movie,
                                    session,
                                    async () =>
                                    {
                                        count -= 1;
                                        if (count == 0)
                                            Finished();
                                        else if (nextTask != null)
                                        {
                                            var currentTask = previousTask;
                                            previousTask = nextTask;
                                            if (currentTask != null)
                                                await currentTask;
                                            nextTask = null;
                                        }
                                    });
                if (isFirst || !started)
                {
                    await nextTask;
                    nextTask = null;
                }
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
                var session = await _DownloadManager.StartDownloadAsync(item.Item, MediaItemCopyType.Cache).ConfigureAwait(false);
                session.PropertyChanged += async (sender, e) =>
                {
                    switch(e.PropertyName)
                    {
                        case nameof(DownloadSession.Status):
                            await UpdateDownloadSourceAsync(source, session);
                            break;
                        case nameof(DownloadSession.Progress):
                            source.SetProgress(session.Progress);
                            break;
                    }
                };
                await UpdateDownloadSourceAsync(source, session);
            });
            return source;
        }
        private async Task UpdateDownloadSourceAsync(DownloadSource source, DownloadSession session)
        {
            switch (session.Status)
            {
                case DownloadStatus.Failed:
                    source.SetError(session.ErrorMessage);
                    break;
                case DownloadStatus.Finished:
                    if (session.Item != null)
                    {
                        var typedItem = await _MediaLibrary.GetTypedItem(session.Item.Id);
                        var mediaSource = MediaSource.FromFile(session.Item.Path);
                        source.SetMediaSource(session.Item, typedItem, mediaSource);
                    }
                    break;
            }            
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
