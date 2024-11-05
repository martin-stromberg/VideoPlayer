using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Playlists;

namespace VideoPlayer.Service.Playlists
{
    public class BasePlaylistService : BaseService
    {        
        private readonly PlaylistType playlistType;
        private Playlist playlist;
        public BasePlaylistService(
            IMediaLibrary mediaLibrary, 
            IMediaCollectionSelector mediaCollectionSelector, 
            PlaylistType playlistType)
        {
            MediaLibrary = mediaLibrary;
            MediaCollectionSelector = mediaCollectionSelector;
            this.playlistType = playlistType;
        }

        protected IMediaLibrary MediaLibrary { get; }
        protected IMediaCollectionSelector MediaCollectionSelector { get; }
        protected Playlist Current { get => playlist ??= InitCurrentPlaylist(); }
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
        protected void SaveChanges()
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
        private void PL_DownloadFailed(object sender, DownloadEventArgs e)
        {
            ExecuteDownloadFailed(e);
        }

        protected virtual void ExecuteDownloadFailed(DownloadEventArgs e)
        {
        }

        protected virtual void ExecuteDownloadRequest(DownloadEventArgs e)
        {
        }

        protected MediaItem FindNextMediaItem(MediaItem mediaItem)
        {
            return MediaCollectionSelector.FindNextMediaItem(mediaItem);
        }
    }
}
