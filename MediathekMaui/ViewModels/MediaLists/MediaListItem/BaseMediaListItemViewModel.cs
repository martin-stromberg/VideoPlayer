using Mediathek.Extensions;
using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists.MediaListItem
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
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager, settingsService)
        {
            MediaLibrary = mediaLibrary;
            mediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdated;
            mediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;

            _DownloadManager = downloadManager;

            // _MediaDownloader.Downloaded += _MediaDownloader_Downloaded;
            // _MediaDownloader.DownloadDeleted += _MediaDownloader_DownloadDeleted;
            Mode = ItemViewModel.Box;
            Item = mediaItem;
            StartPlayback = new Command(() => ExecuteStartPlayback(), () => CanStartPlayback());
            DownloadItem = new Command(() => ExecuteDownloadItem(), () => CanDownloadItem());
            DeleteDownload = new Command(() => ExecuteDeleteDownload(), () => CanDeleteDownload());
            Save = new Command(() => ExecuteSaveNewItem());
            Cancel = new Command(() => ExecuteCancelNewItem());
        }

        public IMediaLibrary MediaLibrary { get; set; }

        private bool IsItem(BaseModel compare)
        {
            if (compare == null)
                return false;
            if (Item == null)
                return false;
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

        private readonly IDownloadManager _DownloadManager;

        public Command DownloadItem { get; }

        public Command DeleteDownload { get; set; }

        private async void ExecuteDownloadItem()
        {
            CanBeDownloaded = false;
            var downloadSession = (await _DownloadManager.StartDownloadAsync(Item, MediaItemCopyType.Download)).FirstOrDefault();
        }

        private bool CanDownloadItem()
        {
            return true;
        }

        private void ExecuteDeleteDownload()
        {
            HasDownload = false;
            CanBeDownloaded = false;
            _DownloadManager.RemoveDownload(Item);
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

        public bool IsNew
        {
            get
            {
                return GetProperty<bool>();
            }
            private set
            {
                SetProperty<bool>(value);
                IsStored = !value;
            }
        }

        public bool IsStored
        {
            get
            {
                return GetProperty<bool>();
            }
            private set
            {
                SetProperty<bool>(value);
                if (value && IsNew)
                    IsNew = false;
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
                    if (int.TryParse((Item as TVShowEpisode)?.Part, out int part) && (part > 1))
                        Title += $"(Teil {(Item as TVShowEpisode).Part})";
                    Subtitle = (Item as TVShowEpisode)?.ShowName ?? string.Empty;
                    break;
                case ItemViewModel.Lane:
                    Title = $"{(Item as TVShowEpisode)?.EpisodeNo} {Item?.Name}".Trim();
                    if (int.TryParse((Item as TVShowEpisode)?.Part, out int currPart) && (currPart > 1))
                        Title += $" (Teil {(Item as TVShowEpisode).Part})";
                    Subtitle = (Item as TVShowEpisode)?.Plot?.Shorten(250);
                    break;
            }
            Path = (Item as MediaItem)?.Path ?? string.Empty;
            HasDownload = (Item as MediaItem)?.HasDownload ?? ((Item as TVShowEpisode)?.DownloadMediaItem) != null;
            Picture = FindProperty<ImageSource>();
            IsNew = (Item != null) && (Item.Id == 0);
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

        public event EventHandler<BaseModelProcessEventArgs> BeforeOpenDetails;

        protected virtual void OnBeforeOpenDetails(BaseModelProcessEventArgs e)
        {
            BeforeOpenDetails?.Invoke(this, e);
        }

        public abstract void OpenDetails();

        public abstract void OpenCategory();

        protected abstract void ExecuteStartPlayback();

        protected abstract bool CanStartPlayback();

        public Command Save { get; set; }

        public Command Cancel { get; set; }

        protected abstract void ExecuteSaveNewItem();

        protected abstract void ExecuteCancelNewItem();

        public bool HasItem(BaseModel item)
        {
            return (Item is not null) && (Item.GetType() == item.GetType()) && (Item.Id == item.Id);
        }

    }
}
