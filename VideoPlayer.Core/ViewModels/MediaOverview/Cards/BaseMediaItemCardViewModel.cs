using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using VideoPlayer.Extensions;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;
using VideoPlayer.ViewModels.Common;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.MediaOverview.Cards
{
    public class BaseMediaItemCardViewModel 
        : BaseCardViewModel
    {
        private bool _SkipPositionEvent;
        private TimeSpan _StartingPosition;
        protected IPlaylistManager PlaylistManager { get; }
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
            CanEdit = entry is MovieCollection;
            HasDownload = downloadableEntry is not null && downloadableEntry.DownloadMediaItemId != 0;
            IsFavorite = PlaylistManager.IsInFavorite(entry);
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
            Subtitle = $"{(Year > 0 ? Year.ToString() : "")}{(Year > 0 && !string.IsNullOrWhiteSpace(Genres) ? " - " : "")}{Genres}";
        }

        public BaseMediaItemCardViewModel(
            IPlaylistManager playlistManager,
            IEnvironment environment,
            IResourceManager resourceManager,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary,
            INavigationManager navigationManager,
            ILogger logger,
            ClassifiedEntry entry)
            :base(mediaLibrary, navigationManager, logger)
        {
            this.PlaylistManager = playlistManager;
            this.environment = environment;
            this.ResourceManager = resourceManager;
            this.downloadManager = downloadManager;
            this.Entry = entry;
            Title = entry.Name;
            VideoSource = null;
            SecondCollectionContext = new MediaCollectionViewModel(logger);
            SecondCollectionContext.Selected += SecondCollectionContext_Selected;
            SecondCollectionContext.Items.CollectionChanged += Items_CollectionChanged;

            PlaybackCommand = new Command(() => ExecutePlaybackCommand());
            ActionCommand = new Command((args) => ExecuteAction((string)args));
        }


        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var isEmpty = SecondCollectionContext.Items.Count == 0;
            SetSecondCollectionVisible(!isEmpty);
        }

        protected void BringToView(ClassifiedEntry entry)
        {
            BringToViewRequest?.Invoke(this, new BaseServiceModelEventArgs(entry));
        }
        public event EventHandler<BaseServiceModelEventArgs> BringToViewRequest;
        public string Subtitle { get => GetProperty<string>(); private set { SetProperty(value); } }
        public bool CanEdit { get => GetProperty<bool>(); private set { SetProperty(value); } }
        public bool HasDownload { get => GetProperty<bool>(); set { SetProperty(value); HasNoDownload = !value; } }
        public bool IsNotFavorite { get => GetProperty<bool>(); set { SetProperty(value); } }
        public bool IsFavorite { get => GetProperty<bool>(); set { SetProperty(value); IsNotFavorite = !value; } }
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
                    case "add_favorite":
                        AddToFavorite(Entry);
                        break;
                    case "remove_favorite":
                        RemoveFromFavorite(Entry);
                        break;
                    case "openProtocol":
                        OpenProtocol(Entry);
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
                downloadManager.Enqueue(entry, MediaItemCopyType.Download, TimeSpan.Zero);
            HasNoDownload = false;
        }
        protected virtual void AddToFavorite(ClassifiedEntry entry)
        {
            if (entry is not null)
                PlaylistManager.AddToFavorite(entry);
            IsFavorite = true;
        }
        protected virtual void RemoveFromFavorite(ClassifiedEntry entry)
        {
            if (entry is not null)
                PlaylistManager.RemoveFromFavorite(entry);
            IsFavorite = false;
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

        protected void StartDownload(TVShowSeason selectedSeason)
        {
            downloadManager.Enqueue(selectedSeason, MediaItemCopyType.Download, TimeSpan.Zero);
        }
        protected virtual void RemoveDownload(ClassifiedEntry entry)
        {
            HasDownload = false;
            downloadManager.RemoveDownloads(entry);
            UpdateMediaInformation(Entry);
        }


        private void SecondCollectionContext_Selected(object sender, BaseViewModelEventArgs e)
        {
            OpenCard((e.ViewModel as BaseListItem));
        }

        
        
        protected override void Select(BaseListItem item)
        {
            Select((item as BaseMediaListItem).Item);            
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

        protected virtual void SetSecondCollectionVisible(bool visible)
        {
            SecondCollectionVisible = visible;
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
            if (PictureBackgroundColor == Colors.White)
                PictureBackgroundColor = Color.FromArgb("#cdcdcd");
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

        public bool PlaybackControlsVisible 
        { 
            get => GetProperty<bool>(); 
            set {
                if (MainThread.IsMainThread)
                    SetProperty(value);
                else 
                    MainThread.BeginInvokeOnMainThread(() => { SetProperty(value); });
            }
        }

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

        public IMediaCollectionViewModel SecondCollectionContext { get; }
        public bool SecondCollectionVisible { get => GetProperty<bool>(); set { SecondCollectionContext.Visible = value; SetProperty<bool>(value); } }
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
            if (VideoSource is not null)
                if (VideoSource is FileMediaSource)
                    if (((FileMediaSource)VideoSource).Path == totalPath)
                        return;
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
            {
                SeekRequested?.Invoke(this, new TimeSpanEventArgs(_StartingPosition));
                _StartingPosition = TimeSpan.Zero;
            }
        }
        public event EventHandler<TimeSpanEventArgs> SeekRequested;

        internal void ExecuteStateChanged(MediaElementState previousState, MediaElementState newState)
        {
            CurrentState = newState;
        }
        protected virtual void Rename(ClassifiedEntry entry, string newName)
        {
            entry.Name = newName;
            MediaLibrary.AddOrUpdateEntry(entry);
            UpdateMediaInformation(entry);
        }
        public void Rename(string newName)
        {
            Rename(Entry, newName);
        }
        #endregion
    }
}
