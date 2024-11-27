using VideoPlayer.Extensions;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Device;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;
using VideoPlayer.ViewModels.Common;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;
using VideoPlayer.Service.Library;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using VideoPlayer.Navigation;
using System.Xml.Serialization;
using System.Runtime.CompilerServices;

namespace VideoPlayer.ViewModels.MediaOverview.Cards
{
    public class BaseMediaItemCardViewModel 
        : BaseViewModel, IEventPublisher, IMultiEventCollection
    {
        private bool _SkipPositionEvent;
        private TimeSpan _StartingPosition;
        protected IPlaylistManager PlaylistManager { get; }
        protected IMediaLibrary MediaLibrary { get; }
        protected ClassifiedEntry Entry 
        { 
            get => GetProperty<ClassifiedEntry>();
            private set
            {
                var old = GetProperty<ClassifiedEntry>();
                if (old is not null)
                    value.PropertyChanged -= Entry_PropertyChanged;
                SetProperty(value);
                if (value is not null)
                    value.PropertyChanged += Entry_PropertyChanged;
                UpdateMediaInformation(Entry);
            }
        }

        private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateMediaInformation(Entry);
        }

        protected virtual void UpdateMediaInformation(ClassifiedEntry entry)
        {
            IDownloadableEntry downloadableEntry = entry as IDownloadableEntry;
            IPlayableEntry playableEntry = entry as IPlayableEntry;
            HasDownload = downloadableEntry is not null && downloadableEntry.DownloadMediaItemId != 0;
            SetPicture(entry as IPicturedEntry);
            Title = entry.Name;
            if (playableEntry is not null)
            {
                Year = playableEntry.ReleaseDate.Year;
                if (Year == 0)
                    Year = playableEntry.PremieredAt.Year;
                Genres = string.Join(", ", playableEntry.Genres);
                Plot = playableEntry.Plot;
            }
            else
            {
                Year = 0;
                Genres = "";
                Plot = "";
            }
        }

        public BaseMediaItemCardViewModel(
            IPlaylistManager playlistManager,
            IEnvironment environment,
            IResourceManager resourceManager,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary,
            INavigationManager navigationManager,
            ClassifiedEntry entry)
        {
            this.PlaylistManager = playlistManager;
            this.environment = environment;
            this.ResourceManager = resourceManager;
            this.downloadManager = downloadManager;
            this.MediaLibrary = mediaLibrary;
            this.navigationManager = navigationManager;
            this.Entry = entry;
            Title = entry.Name;
            VideoSource = null;
            CollectionContext = new MediaCollectionViewModel();
            CollectionContext.Selected += CollectionContext_Selected;
            CollectionContext.Items.CollectionChanged += CollectionContext_Items_CollectionChanged;
            PlaybackCommand = new Command(() => ExecutePlaybackCommand());
            ActionCommand = new Command((args) => ExecuteAction((string)args));
        }

        public IEnumerable<IEventSubscriber> GetSubscribers()
        {
            return new IEventSubscriber[] { MediaLibrary as IEventSubscriber };
        }

        public IEnumerable<IEventPublisher> GetPublishers()
        {
            return new IEventPublisher[] { MediaLibrary as IEventPublisher };
        }


        protected void BringToView(ClassifiedEntry entry)
        {
            BringToViewRequest?.Invoke(this, new BaseServiceModelEventArgs(entry));
        }
        public event EventHandler<BaseServiceModelEventArgs> BringToViewRequest;

        public bool HasDownload { get => GetProperty<bool>(); set { SetProperty(value); HasNoDownload = !value; } }
        public bool HasNoDownload { get => GetProperty<bool>(); private set { SetProperty(value); } }
        protected virtual void ExecuteAction(string args)
        {
            try
            {
                switch ((string)args)
                {
                    case "delete":
                        RemoveDownload(Entry);
                        break;
                    case "rescan":
                        Rescan(Entry);
                        break;
                    case "reload":
                        Reload(Entry);
                        break;
                    case "download":
                        Download(Entry);
                        break;
                }
            }
            catch(Exception ex)
            {
                NotifyError(ex);
            }
        }

        protected virtual void Download(ClassifiedEntry entry)
        {
            if (entry is not null)
                downloadManager.Enqueue(entry, MediaItemCopyType.Download);
            HasNoDownload = false;
        }

        protected virtual void Rescan(ClassifiedEntry entry)
        {
            Notify(this, new Service.Events.NotificationEventArgs("Rescan", entry));
        }
        protected virtual void Reload(ClassifiedEntry entry)
        {
            Entry.Visible = false;
            Notify(this, new Service.Events.NotificationEventArgs("Reload", entry));
            Close();
        }
        protected virtual void Close ()
        {           
            
            navigationManager.CloseCurrentPage();
        }

        protected void StartDownload(TVShowSeason selectedSeason)
        {
            downloadManager.Enqueue(selectedSeason, MediaItemCopyType.Download);
        }
        protected virtual void RemoveDownload(ClassifiedEntry entry)
        {
            HasDownload = false;
            downloadManager.RemoveDownloads(entry);
            UpdateMediaInformation(Entry);
        }

        private void CollectionContext_Selected(object sender, BaseViewModelEventArgs e)
        {
            Select((e.ViewModel as BaseMediaListItem).Item);
        }

        protected virtual void Select(ClassifiedEntry item)
        {
            UpdateMediaInformation(item ?? Entry);
        }
        protected virtual void PlayLoadingVideo()
        {
            PlaybackControlsVisible = false;
            _SkipPositionEvent = true;
            _StartingPosition = TimeSpan.Zero;
            VideoSource = ResourceManager.GetLoadingVideo();
        }
        protected virtual void ExecutePlaybackCommand()
        {
            try
            {
                PlayLoadingVideo();
                StartPlayback(Entry);
            }
            catch(Exception ex)
            {
                NotifyError(ex);
            }
        }

        private void CollectionContext_Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var isEmpty = CollectionContext.Items.Count == 0;            
            SetCollectionVisible(!isEmpty);
        }

        protected virtual void SetCollectionVisible(bool visible)
        {
            CollectionVisible = visible;
        }

        protected void SetPicture(IPicturedEntry picturedEntry)
        {            
            if (picturedEntry is null) return;
            var cacheFolder = FileSystem.Current.AppDataDirectory;
            string picturePath = string.Empty;
            if (!string.IsNullOrWhiteSpace(picturedEntry.BannerPath))
            {
                picturePath = PathTools.Combine(cacheFolder, picturedEntry.BannerPath);
                PictureBackgroundColor = Color.FromRgba(picturedEntry.BannerBackgroundColor);
                PictureTextColor = PictureBackgroundColor.GetContrastingTextColor();
            }
            else if (!string.IsNullOrWhiteSpace(picturedEntry.PicturePath))
            {
                picturePath = PathTools.Combine(cacheFolder, picturedEntry.PicturePath);
                PictureBackgroundColor = Color.FromRgba(picturedEntry.PictureBackgroundColor);
                PictureTextColor = PictureBackgroundColor.GetContrastingTextColor();
            }
            if (File.Exists(picturePath))
                Picture = ImageSource.FromFile(picturePath);
            else
                Picture = null;
        }

        public ImageSource Picture 
        { get => GetProperty<ImageSource>(); set => SetProperty(value); }


        public Color PictureBackgroundColor { get => GetProperty<Color>(); set => SetProperty(value); }
        public Color PictureTextColor { get => GetProperty<Color>(); set => SetProperty(value); }
        public string Name { get => GetProperty<string>(); protected set => SetProperty(value); }
        public int Year { get => GetProperty<int>(); protected set => SetProperty(value); }
        public string Genres { get => GetProperty<string>(); protected set => SetProperty(value); }
        public string Plot { get => GetProperty<string>(); protected set => SetProperty(value); }

        public bool PlaybackControlsVisible { get => GetProperty<bool>(); set { SetProperty(value); } }

        private Service.Library.Models.MediaItem currentMediaItem;

        public CommunityToolkit.Maui.Views.MediaSource VideoSource { get => GetProperty<CommunityToolkit.Maui.Views.MediaSource>(); set { SetProperty(value); VideoPlayerVisible = value is not null; } }
        public bool VideoPlayerVisible { get => GetProperty<bool>(); set { SetProperty(value); PictureVisible = !value; } }
        public bool PictureVisible { get => GetProperty<bool>(); set => SetProperty(value); }


        protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            base.OnPropertyChanged(propertyName);
            switch(propertyName)
            {
                case nameof(IsAppeared):
                    if (IsAppeared)
                        CheckOnAppearedAction();
                    break;
            }
        }

        private void CheckOnAppearedAction()
        {
            if (_StartPlaybackOnAppeared is not null)
            {
                ExecutePlaybackRequest(_StartPlaybackOnAppeared);
                _StartPlaybackOnAppeared = null;
            }
        }

        protected void StartPlayback(ClassifiedEntry entry)
        {
            PlaylistManager.Play(entry);
        }
        protected void StopVideo()
        {
            PlaybackControlsVisible = false;
            VideoSource = null;
        }

        public IMediaCollectionViewModel CollectionContext { get; }
        public bool CollectionVisible { get => GetProperty<bool>(); set { CollectionContext.Visible = value;  SetProperty<bool>(value); } }
        public Command PlaybackCommand { get; }
        public Command ActionCommand { get; }
        public decimal DownloadProgress { get => GetProperty<decimal>(); set => SetProperty(value); }
        public bool IsDownloadProgressVisible { get => GetProperty<bool>(); set => SetProperty(value); }
        public MediaElementState CurrentState { get; private set; }
        public bool AutoPlay { get => GetProperty<bool>(); set => SetProperty(value); }
        public string Message { get => GetProperty<string>(); set { SetProperty(value); HasMessage = !string.IsNullOrWhiteSpace(value); } }
        public bool HasMessage { get => GetProperty<bool>(); private set => SetProperty(value); }

        public override void ExecuteAppeared()
        {
            PlaylistManager.PlaybackRequest += PlaylistManager_PlaybackRequest;
            PlaylistManager.Downloading += PlaylistManager_Downloading;
            PlaylistManager.DownloadFailed += PlaylistManager_DownloadFailed;
            base.ExecuteAppeared();
            UpdateMediaInformation(Entry);
            PlaylistManager.DownloadProgressChanged += PlaylistManager_DownloadProgressChanged;
            Notify("ProcessStarted");
        }

        private void PlaylistManager_DownloadFailed(object sender, DownloadFailedEventArgs e)
        {
            Message = e.Error.Message;
        }

        private void PlaylistManager_DownloadProgressChanged(object sender, DownloadProgressEventArgs e)
        {
            DownloadProgress = e.Progress;
            IsDownloadProgressVisible = e.Progress > 0 && e.Progress < 100;
        }

        private void PlaylistManager_Downloading(object sender, BaseServiceModelEventArgs e)
        {
            Message = string.Empty;
            IsDownloadProgressVisible = false;
            if (!IsAppeared)
                return;
            if (CurrentState == MediaElementState.Stopped)
            {
                PlaybackControlsVisible = false;
                _SkipPositionEvent = true;
                VideoSource = ResourceManager.GetLoadingVideo();
            }
        }

        private void PlaylistManager_PlaybackRequest(object sender, Service.Library.Models.BaseServiceModelEventArgs e)
        {
            Message = string.Empty;
            IsDownloadProgressVisible = false;
            if (IsAppeared)
                ExecutePlaybackRequest(e.ModelObject as Service.Library.Models.Playlists.PlaylistEntry);
            else
                _StartPlaybackOnAppeared = e.ModelObject as Service.Library.Models.Playlists.PlaylistEntry;
        }

        private void ExecutePlaybackRequest(PlaylistEntry playlistEntry)
        {
            if (!MainThread.IsMainThread)
                MainThread.InvokeOnMainThreadAsync(() => { ExecutePlaybackRequest(playlistEntry); });
            else if (playlistEntry is not null)
                ExecutePlaybackRequest(playlistEntry.Item, playlistEntry.Entry);
            else
                VideoSource = null;
        }

        protected virtual void ExecutePlaybackRequest(
            Service.Library.Models.MediaItem mediaItem,
            Service.Library.Models.Classified.ClassifiedEntry classifiedEntry)
        {
            StartPlayback(mediaItem, classifiedEntry);
        }
        protected void StartPlayback(Service.Library.Models.MediaItem mediaItem, ClassifiedEntry classifiedEntry)
        {
            var totalPath = mediaItem.Path;
            switch(mediaItem.CopyType)
            {
                case MediaItemCopyType.Download:
                case MediaItemCopyType.Cache:
                    totalPath = PathTools.Combine(environment.GetRootPath(), mediaItem.Path);
                    break;
            }
            PlaybackControlsVisible = true;
            currentMediaItem = mediaItem;
            _SkipPositionEvent = false;
            _StartingPosition = mediaItem.LastPosition;
            VideoSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(totalPath);
        }

        public override void ExecuteDisappeared()
        {
            Notify("ProcessFinished");
            PlaylistManager.PlaybackRequest -= PlaylistManager_PlaybackRequest;
            PlaylistManager.Downloading -= PlaylistManager_Downloading;
            base.ExecuteDisappeared();
        }
        protected override void ExecuteFirstAppeared()
        {
            base.ExecuteFirstAppeared();
            if (AutoPlay)
                Task.Run(() => { ExecutePlaybackCommand(); });
        }
        public override void ExecuteNavigatingFrom()
        {
            base.ExecuteNavigatingFrom();
            StopVideo();
        }
        #region IEventPublisher
        private Timer _StatusTimer = null;
        private string _LastStatus = string.Empty;
        private string _PreviousStatus = string.Empty;
        private PlaylistEntry _StartPlaybackOnAppeared;
        private readonly IEnvironment environment;
        protected IResourceManager ResourceManager { get; }
        private readonly IDownloadManager downloadManager;
        private readonly INavigationManager navigationManager;

        public event EventHandler<NotificationEventArgs> OnEvent;

        public virtual void Notify(string msgName)
        {
            Notify(this, new NotificationEventArgs(msgName, null));
        }
        public virtual void Notify(object sender, NotificationEventArgs e)
        {
            OnEvent?.Invoke(sender, e);
        }

        public virtual void NotifyError(Exception error)
        {
            Notify(this, new NotificationEventArgs("Error", error));
        }

        public virtual void NotifyStatus(string message, bool direct = false)
        {
            _LastStatus = message;
            if (direct)
                SendStatus();
            else if (_StatusTimer is null)
                _StatusTimer = new Timer((args) => { SendStatus(); }, null, 1000, 1000);
        }

        private void SendStatus()
        {
            try
            {
                var currentStatus = _LastStatus;
                _LastStatus = string.Empty;
                if (string.IsNullOrWhiteSpace(currentStatus) && _StatusTimer is not null)
                {
                    _StatusTimer.Dispose();
                    _StatusTimer = null;
                }
                if (!string.IsNullOrWhiteSpace(currentStatus) || !string.IsNullOrWhiteSpace(_PreviousStatus))
                    Notify(this, new NotificationEventArgs("Status", currentStatus));
                _PreviousStatus = currentStatus;
            }
            catch { }
        }


        #endregion

        #region VideoEvents 
        public void ExecutePositionChanged(TimeSpan position, TimeSpan duration)
        {
            if (!_SkipPositionEvent)
                PlaylistManager.ProcessVideoPosition(currentMediaItem, position, duration);
        }
        public void ExecuteMediaEnded()
        {
            if (!_SkipPositionEvent)
                PlaylistManager.ProcessMediaEnded(currentMediaItem);
        }
        public void ExecuteMediaOpened()
        {
            if (_StartingPosition != TimeSpan.Zero)
                SeekRequested?.Invoke(this, new TimeSpanEventArgs(_StartingPosition));                
        }
        public event EventHandler<TimeSpanEventArgs> SeekRequested;

        internal void ExecuteStateChanged(MediaElementState previousState, MediaElementState newState)
        {
            CurrentState = newState;
        }
        #endregion
    }
}
