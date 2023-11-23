using System;
using System.Linq;
using VideoPlayer.Extensions;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public enum ItemViewModel
    {

        Box,
        Lane,
        Dummy

    }

    public abstract class BaseMediaListItemViewModel: BaseViewModel
    {

        public BaseMediaListItemViewModel(
            BaseModel mediaItem,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            IMediaDownloader mediaDownloader,
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager, settingsService)
        {
            MediaLibrary = mediaLibrary;
            mediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdated;
            mediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;

            _MediaDownloader = mediaDownloader;

            // _MediaDownloader.Downloaded += _MediaDownloader_Downloaded;
            // _MediaDownloader.DownloadDeleted += _MediaDownloader_DownloadDeleted;
            Mode = ItemViewModel.Box;
            Item = mediaItem;
            StartPlayback = new Command(() => ExecuteStartPlayback(), () => CanStartPlayback());
            DownloadItem = new Command(() => ExecuteDownloadItem(), () => CanDownloadItem());
            DeleteDownload = new Command(() => ExecuteDeleteDownload(), () => CanDeleteDownload());
        }

        public IMediaLibrary MediaLibrary { get; set; }

        private bool IsItem(BaseModel compare)
        {
            if (compare.Id != Item.Id)
                return false;
            return compare.GetType() == Item.GetType();
        }

        private void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            if (!IsItem(e.Element))
                return;
            Item = null;
        }

        private void MediaLibrary_ModelElementUpdated(object sender, BaseModelEventArgs e)
        {
            if (!IsItem(e.Element))
                return;
            Item = e.Element;
        }

        private void _MediaDownloader_DownloadDeleted(object sender, BaseModelEventArgs e)
        {
            var mediaItem = e.Element as MediaItem;
            ProcessDownloadDeleted(Item as TVShowEpisode, mediaItem);
        }

        private void ProcessDownloadDeleted(TVShowEpisode episode, MediaItem mediaItem)
        {
            if ((episode is null) || (mediaItem is null))
                return;
            if (episode.MediaItems.Contains(mediaItem.Id))
                HasDownload = false;
        }

        private void _MediaDownloader_Downloaded(object sender, BaseModelEventArgs e)
        {
            var mediaItem = e.Element as MediaItem;
            ProcessDownload(Item as TVShowEpisode, mediaItem);
        }

        private void ProcessDownload(TVShowEpisode episode, MediaItem mediaItem)
        {
            if (episode is null)
                return;
            if (episode.MediaItems.Contains(mediaItem.Id))
                HasDownload = true;
        }

        private readonly IMediaDownloader _MediaDownloader;

        public Command DownloadItem { get; }

        public Command DeleteDownload { get; set; }

        private void ExecuteDownloadItem()
        {
            CanBeDownloaded = false;
            _MediaDownloader.StartDownload(Item);
        }

        private bool CanDownloadItem()
        {
            return true;
        }

        private void ExecuteDeleteDownload()
        {
            HasDownload = false;
            CanBeDownloaded = false;
            _MediaDownloader.RemoveDownload(Item);
        }

        private bool CanDeleteDownload()
        {
            return true;// (Item as MediaItem)?.HasDownload ?? (Item as TVShowEpisode)?.DownloadMediaItem is not null;
        }

        public ItemViewModel Mode
        {
            get
            {
                return GetProperty<ItemViewModel>();
            }
            set
            {
                SetProperty<ItemViewModel>(value);
                IsBoxMode = value == ItemViewModel.Box;
                IsLaneMode = value == ItemViewModel.Lane;
                IsDummyMode = value == ItemViewModel.Dummy;
                UpdateProperties();
            }

        }

        public bool IsBoxMode
        {
            get
            {
                return GetProperty<bool>();
            }
            private set
            {
                SetProperty<bool>(value);
            }
        }

        public bool IsLaneMode
        {
            get
            {
                return GetProperty<bool>();
            }
            private set
            {
                SetProperty<bool>(value);
            }
        }

        public bool IsDummyMode
        {
            get
            {
                return GetProperty<bool>();
            }
            private set
            {
                SetProperty<bool>(value);
            }
        }

        protected Type ItemType
        {
            get
            {
                return GetProperty<Type>();
            }
            set
            {
                SetProperty<Type>(value);
            }
        }

        public BaseModel Item
        {
            get
            {
                return GetProperty<BaseModel>();
            }
            set
            {
                SetProperty<BaseModel>(value);
                UpdateProperties();
            }
        }

        public ImageSource Picture
        {
            get
            {
                return GetProperty<ImageSource>();
            }
            set
            {
                SetProperty<ImageSource>(value);
            }
        }

        public string Path
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public bool HasDownload
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                CanBeDownloaded = !value;
                if (DeleteDownload != null)
                    DeleteDownload.ChangeCanExecute();
            }
        }

        public bool CanBeDownloaded
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (DownloadItem != null)
                    DownloadItem.ChangeCanExecute();
            }
        }

        public Command StartPlayback { get; set; }

        protected virtual void UpdateProperties()
        {
            ItemType = Item?.GetType();
            switch (Mode)
            {
                case ItemViewModel.Box:
                    Title = $"{(Item as TVShowEpisode)?.SeasonName} {(Item as TVShowEpisode)?.EpisodeNo}".Trim();
                    if (string.IsNullOrWhiteSpace(Title))
                        Title = Item?.Name ?? string.Empty;
                    Subtitle = (Item as TVShowEpisode)?.ShowName ?? string.Empty;
                    break;
                case ItemViewModel.Lane:
                    Title = $"{(Item as TVShowEpisode)?.EpisodeNo} {Item?.Name}".Trim();
                    Subtitle = (Item as TVShowEpisode).Plot?.Shorten(250);
                    break;
            }
            Path = (Item as MediaItem)?.Path ?? string.Empty;
            HasDownload = (Item as MediaItem)?.HasDownload ?? ((Item as TVShowEpisode)?.DownloadMediaItem) != null;
            Picture = FindProperty<ImageSource>();
        }

        private T FindProperty<T>()
        {
            if (ItemType == null)
                return default(T);
            var prop = ItemType.GetProperties().FirstOrDefault(p => p.CanRead && (p.PropertyType == typeof(T)));
            if (prop == null)
                return default(T);
            return (T)prop.GetValue(Item);
        }

        public abstract void OpenDetails();

        public abstract void OpenCategory();

        protected abstract void ExecuteStartPlayback();

        protected abstract bool CanStartPlayback();

    }
}
