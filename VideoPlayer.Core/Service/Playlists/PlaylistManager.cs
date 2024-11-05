using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Resources;

namespace VideoPlayer.Service.Playlists
{
    public interface IPlaylistManager
    {
        NextPlaybackPlaylist NextPlaybackPlaylist { get; }
        event EventHandler<BaseServiceModelEventArgs> PlaybackRequest;
        event EventHandler<BaseServiceModelEventArgs> Downloading;
        event EventHandler<DownloadProgressEventArgs> DownloadProgressChanged;
        void Init();
        void Play(ClassifiedEntry movie);
        void ProcessMediaEnded(MediaItem currentMediaItem);
        void ProcessVideoPosition(MediaItem currentMediaItem, TimeSpan position, TimeSpan duration);
        void Reset();
    }
    public class PlaylistManager: BaseService, IPlaylistManager
    {
        public PlaylistManager(
            IMediaLibrary mediaLibrary, 
            IDownloadManager downloadManager,
            IMediaCollectionSelector mediaCollectionSelector)
            :base()
        {
            General = new GeneralPlaylist(mediaLibrary, downloadManager, mediaCollectionSelector);
            General.PlaybackRequest += General_PlaybackRequest;
            General.DownloadStarting += General_DownloadStarting;
            General.DownloadProgress += General_DownloadProgress;

            NextPlaybackPlaylist = new NextPlaybackPlaylist(mediaLibrary, mediaCollectionSelector);
        }

        private void General_DownloadProgress(object sender, DownloadProgressEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }

        private void General_DownloadStarting(object sender, DownloadEventArgs e)
        {
            Downloading?.Invoke(this, new BaseServiceModelEventArgs(e.ModelObject));
        }

        public event EventHandler<BaseServiceModelEventArgs> PlaybackRequest;
        public event EventHandler<BaseServiceModelEventArgs> Downloading;
        public event EventHandler<DownloadProgressEventArgs> DownloadProgressChanged;
        private void General_PlaybackRequest(object sender, Library.Models.Playlists.PlaylistEntry e)
        {
            PlaybackRequest?.Invoke(this, new BaseServiceModelEventArgs(e));
        }

        protected GeneralPlaylist General { get; set; }
        public NextPlaybackPlaylist NextPlaybackPlaylist { get; protected set; }
        public void Init()
        {
            General.Init();
            NextPlaybackPlaylist.Init();
        }
        public void Reset()
        {
            General.Reset();
            NextPlaybackPlaylist.Reset();
        }

        public void Play(ClassifiedEntry movie)
        {
            General.Start(movie);
        }

        public void ProcessVideoPosition(MediaItem currentMediaItem, TimeSpan position, TimeSpan duration)
        {
            NextPlaybackPlaylist.ProcessVideoPosition(currentMediaItem, position, duration);
        }
        public void ProcessMediaEnded(MediaItem currentMediaItem)
        {
            General.Continue(currentMediaItem);
        }
    }
}
