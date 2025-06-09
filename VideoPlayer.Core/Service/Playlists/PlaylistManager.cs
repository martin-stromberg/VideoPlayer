using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Tenants;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Resources;
using VideoPlayer.Service.Settings;

namespace VideoPlayer.Service.Playlists
{
    public interface IPlaylistManager
    {
        NextPlaybackPlaylist NextPlaybackPlaylist { get; }
        FavoritePlaylist Favorites { get; }
        NewEntriesPlaylist NewPlaylist { get; }
        event EventHandler<BaseServiceModelEventArgs> PlaybackRequest;
        event EventHandler<BaseServiceModelEventArgs> Downloading;
        event EventHandler<DownloadProgressEventArgs> DownloadProgressChanged;
        event EventHandler<DownloadFailedEventArgs> DownloadFailed;
        void Init();
        void Play(ClassifiedEntry movie);
        void ProcessMediaEnded(MediaItem currentMediaItem);
        void ProcessVideoPosition(MediaItem currentMediaItem, TimeSpan position, TimeSpan duration);
        void Reset();
        void AddToFavorite(ClassifiedEntry entry);
        void RemoveFromFavorite(ClassifiedEntry entry);
        bool IsInFavorite(ClassifiedEntry entry);
        void CheckAndUpdateDueTimes();
    }
    public class PlaylistManager: BaseService, IPlaylistManager
    {
        public PlaylistManager(
            ITenantSelection tenantSelection,
            IMediaLibrary mediaLibrary, 
            IDownloadManager downloadManager,
            IProcessorCollection processorCollection,
            IMediaCollectionSelector mediaCollectionSelector,
            IApplicationSettings applicationSettings,
            ILogger<PlaylistManager> logger)
            :base(logger)
        {
            General = new GeneralPlaylist(mediaLibrary, downloadManager, mediaCollectionSelector, Logger);
            General.PlaybackRequest += General_PlaybackRequest;
            General.DownloadStarting += General_DownloadStarting;
            General.DownloadProgress += General_DownloadProgress;
            General.DownloadFailed += General_DownloadFailed;

            NextPlaybackPlaylist = new NextPlaybackPlaylist(mediaLibrary, mediaCollectionSelector, processorCollection, downloadManager, applicationSettings, Logger);
            NewPlaylist = new NewEntriesPlaylist(tenantSelection, mediaLibrary, mediaCollectionSelector, downloadManager, Logger);
            Favorites = new FavoritePlaylist(mediaLibrary, mediaCollectionSelector, downloadManager,logger);
        }

        private void General_DownloadFailed(object sender, DownloadFailedEventArgs e)
        {
            DownloadFailed?.Invoke(this, e);
        }

        public override IEnumerable<IEventPublisher> GetPublishers()
        {
            return base.GetPublishers()
                .Concat(new IEventPublisher[] { NewPlaylist, NextPlaybackPlaylist, Favorites });
        }
        public override IEnumerable<IEventSubscriber> GetSubscribers()
        {
            return base.GetSubscribers()
                .Concat(new IEventSubscriber[] { NewPlaylist, NextPlaybackPlaylist, Favorites });
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
        public event EventHandler<DownloadFailedEventArgs> DownloadFailed;
        private void General_PlaybackRequest(object sender, Library.Models.Playlists.PlaylistEntry e)
        {
            PlaybackRequest?.Invoke(this, new BaseServiceModelEventArgs(e));
        }

        protected GeneralPlaylist General { get; set; }
        public NextPlaybackPlaylist NextPlaybackPlaylist { get; protected set; }
        public FavoritePlaylist Favorites { get; protected set; }
        public void Init()
        {
            General.Init();
            NextPlaybackPlaylist.Init();
            NewPlaylist.Init();
            Favorites.Init();
        }
        public void Reset()
        {
            General.Reset();
            NextPlaybackPlaylist.Reset();
            NewPlaylist.Reset();
            Favorites.Reset();
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

        public void CheckAndUpdateDueTimes()
        {
            NextPlaybackPlaylist.CheckAndUpdateDueTimes();
        }

        #region NewEntriesPlaylist
        public NewEntriesPlaylist NewPlaylist { get; }
        #endregion

        #region Favorites
        public void AddToFavorite(ClassifiedEntry entry)
        {
            Favorites.Add(entry);
        }
        public void RemoveFromFavorite(ClassifiedEntry entry)
        {
            Favorites.Remove(entry);
        }
        public bool IsInFavorite(ClassifiedEntry entry)
        {
            return Favorites.Contains(entry);
        }
        #endregion
    }
}
