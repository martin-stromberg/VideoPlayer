using Microsoft.Extensions.Logging;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;

namespace VideoPlayer.Service.Playlists
{
    public class BasePlaylistService : BaseService
    {
        private readonly IDownloadManager downloadManager;
        private readonly PlaylistType playlistType;
        private Playlist playlist;
        public BasePlaylistService(
            IMediaLibrary mediaLibrary, 
            IMediaCollectionSelector mediaCollectionSelector,
            IDownloadManager downloadManager,
            PlaylistType playlistType,
            ILogger logger)
            :base(logger)
        {
            MediaLibrary = mediaLibrary;
            MediaCollectionSelector = mediaCollectionSelector;
            this.downloadManager = downloadManager;
            this.playlistType = playlistType;
        }
        public bool CorrectInvisibleMediaItems { get; set; }
        protected IMediaLibrary MediaLibrary { get; }
        protected IMediaCollectionSelector MediaCollectionSelector { get; }
        public Playlist Current { get => playlist ??= InitCurrentPlaylist(); }
        internal void Reset()
        {
            Current.Items.Clear();
            playlist = null;
            Init();
        }
        internal void Init()
        {
            _ = Current.Items.Any();
        }
        protected virtual void SaveChanges()
        {
            MediaLibrary.AddOrUpdatePlaylist(Current);
        }
        private Playlist CreatePlaylist()
        {
            return new Playlist(null)
            {
                AutoDownload = playlistType == PlaylistType.General,
                Name = playlistType.ToString(),
                Type = playlistType,
                BagMode = playlistType == PlaylistType.NextPlayback
            };
        }
        protected virtual Playlist InitCurrentPlaylist()
        {
            var pl = MediaLibrary
                .GetPlaylists(playlistType)
                .FirstOrDefault();
            if (pl is null)
                pl = CreatePlaylist();
            try
            {
                if (CorrectInvisibleMediaItems)
                    DoCorrectInvisibleMediaItems(pl);

                pl.Items.CollectionChanged += Items_CollectionChanged;
                pl.DownloadRequested += Pl_DownloadRequested; ;
                pl.DownloadFailed += PL_DownloadFailed;
                pl.PlaybackRequest += Pl_PlaybackRequest;
                pl.AutoDownload = true;
                return pl;
            }
            finally
            {
                PlaylistLoaded?.Invoke(this, new BaseServiceModelEventArgs(pl));
            }
        }

        private void DoCorrectInvisibleMediaItems(Playlist pl)
        {
            foreach (var item in pl.Items)
                DoCorrectInvisibleMediaItems(item);
        }

        private void DoCorrectInvisibleMediaItems(PlaylistEntry item)
        {
            if (item.Entry is null) return;
            if (item.Entry.Visible) return;
            if (item.Entry is MovieCollection)
            {
                var movies = MediaLibrary.GetCollectionMovies(item.Entry.Id).ToList();
                try
                {
                    var movie = movies.FirstOrDefault();
                    if (movie is null) return;
                    movies.Remove(movie);
                    MediaLibrary.Release(item.Entry);
                    item.Entry = movie;
                }
                finally { MediaLibrary.Release(movies); }
            }
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
                foreach (var item in e.NewItems.OfType<PlaylistEntry>())
                {                    
                    MediaLibrary.Hold(item.Item);
                    MediaLibrary.Hold(item.Entry);
                }
            if (e.OldItems is not null)
                foreach (var item in e.OldItems.OfType<PlaylistEntry>())
                {
                    MediaLibrary.Release(item.Item);
                    MediaLibrary.Release(item.Entry);
                }
        }

        public event EventHandler<BaseServiceModelEventArgs> PlaylistLoaded;

        private void Pl_PlaybackRequest(object sender, PlaylistEntry e)
        {
            ExecutePlaybackRequest(e);
        }

        protected virtual void ExecutePlaybackRequest(PlaylistEntry e)
        {
        }

        private void Pl_DownloadRequested(object sender, Download.DownloadEventArgs e)
        {
            ExecuteDownloadRequest(e);
        }
        private void PL_DownloadFailed(object sender, DownloadFailedEventArgs e)
        {
            ExecuteDownloadFailed(e);
        }

        protected virtual void ExecuteDownloadFailed(DownloadFailedEventArgs e)
        {
        }

        protected virtual void ExecuteDownloadRequest(DownloadEventArgs e)
        {
            var entry = e.ModelObject as PlaylistEntry;
            e.Session = downloadManager.Enqueue(entry.Entry, entry.Item, TimeSpan.Zero);
            e.Session.Finished += Session_Finished;
        }
        protected virtual void ExecuteDownloadRequest(MediaItem item, TimeSpan dueTime)
        {
            var session = downloadManager.Enqueue(null, item, dueTime);
            session.Finished += Session_Finished;
        }

        private void Session_Finished(object sender, DownloadEventArgs e)
        {
            e.Session.Finished -= Session_Finished;
            ExecuteDownloadFinished(e.Session.Entry, e.Session.Item);
        }

        protected virtual void ExecuteDownloadFinished(ClassifiedEntry entry, MediaItem item)
        {
            
        }

        
        protected MediaItem FindNextMediaItem(MediaItem mediaItem)
        {
            return MediaCollectionSelector.FindNextMediaItem(mediaItem);
        }
    }
}
