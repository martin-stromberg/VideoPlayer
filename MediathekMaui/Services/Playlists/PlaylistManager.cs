using CommunityToolkit.Maui.Views;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;

namespace Mediathek.Services.Playlists
{
    public class PlaylistManager: BaseManager, IPlaylistManager
    {

        private readonly MediaLibraryEnvironment _Settings;
        private readonly IDownloadManager _DownloadManager;

        public PlaylistManager(
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            MediaLibraryEnvironment settings)
            : base(mediaLibrary)
        {
            _Settings = settings;
            _DownloadManager = downloadManager;
            GeneralPlaylist = new GeneralPlaylist($"Allgemein", mediaLibrary);
        }

        public async Task InitializeAsync()
        {
            await GeneralPlaylist.InitializeAsync();
        }

        private async Task<MediaItem> GetFirstMediaItem(long[] mediaItems)
        {
            MediaItem item = null;
            foreach (var mediaItemId in mediaItems)
            {
                item = await MediaLibrary.GetMediaItemAsync(mediaItemId);
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
            await GeneralPlaylist.StartPlaylist(episode, GetCollectionElements);
        }

        public async Task StartTVShowPlaylistAsync(TVShow show)
        {
            await GeneralPlaylist.StartPlaylist(show);
        }

        public async Task StartTVShowPlaylistAsync(TVShowSeason season)
        {
            await GeneralPlaylist.StartPlaylist(season);
        }

        public async Task StartMoviePlaylistAsync(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements)
        {
            await GeneralPlaylist.StartPlaylist(movie);
        }

        public async Task StartMoviePlaylistAsync(MovieCollection movieCollection)
        {
            await GeneralPlaylist.StartPlaylist(movieCollection);
        }

        public async Task StartMediaItemPlaylistAsync(MediaItem mediaItem)
        {
            await GeneralPlaylist.StartPlaylist(mediaItem);
        }

        public Task StartPlaybackAsync()
        {
            return Task.CompletedTask;
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
            var item = GeneralPlaylist.Playlist.Items.FirstOrDefault();
            if (item == null)
                return null;
            if (string.IsNullOrWhiteSpace(loadingMoviePath))
                loadingMoviePath = findLocalFile("loading.mp4");
            DownloadSource source = new DownloadSource();
            source.SetMediaSource(null, null, MediaSource.FromFile(loadingMoviePath));

            Task.Run(async () =>
            {
                var session = await _DownloadManager.StartDownloadAsync(item.Item, MediaItemCopyType.Cache)
                                                    .ConfigureAwait(false);
                session.PropertyChanged += async (sender, e) =>
                {
                    switch (e.PropertyName)
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
                        var typedItem = await MediaLibrary.GetTypedItem(session.Item.Id);
                        if (typedItem == null)
                            typedItem = await MediaLibrary.GetTypedItem(session.Item.OriginalMediaItemId);
                        var mediaSource = MediaSource.FromFile(session.Item.Path);
                        source.SetMediaSource(session.Item, typedItem, mediaSource);
                    }
                    break;
            }
        }

        public async Task<DownloadSource> ProcessMediaEndedAsync(MediaItem item)
        {
            await GeneralPlaylist.ProcessMediaEndedAsync(item);
            return GetFirstVideoSource();
        }

        public GeneralPlaylist GeneralPlaylist { get; }

        private async Task SavePlaylistAsync(Playlist playlist)
        {
            await MediaLibrary.AddPlaylistAsync(playlist);
        }

        public void ProcessMediaFailed(MediaItem item)
        {
            _DownloadManager.RemoveDownload(item);
        }

    }
}
