using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public enum ItemViewModel
    {

        Box,
        Lane

    }

    public abstract class BaseMediaListItemViewModel: BaseViewModel
    {

        public BaseMediaListItemViewModel(
            BaseModel mediaItem,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            IMediaDownloader mediaDownloader)
            : base(statusPublisher, navigationManager, settingsService)
        {
            _MediaDownloader = mediaDownloader;
            Mode = ItemViewModel.Box;
            Item = mediaItem;
            StartPlayback = new Command(() => ExecuteStartPlayback(), () => CanStartPlayback());
            DownloadItem = new Command(() => ExecuteDownloadItem(), () => CanDownloadItem());
            DeleteDownload = new Command(() => ExecuteDeleteDownload(), () => CanDeleteDownload());
        }

        private readonly IMediaDownloader _MediaDownloader;

        public Command DownloadItem { get; }

        public Command DeleteDownload { get; set; }

        private void ExecuteDownloadItem()
        {
            _MediaDownloader.StartDownload(Item);
        }

        private bool CanDownloadItem()
        {
            return true;
        }

        private void ExecuteDeleteDownload()
        {
            _MediaDownloader.RemoveDownload(Item);
        }

        private bool CanDeleteDownload()
        {
            return (Item as MediaItem)?.HasDownload ?? false;
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
                if (DeleteDownload != null)
                    DeleteDownload.ChangeCanExecute();
            }
        }

        public Command StartPlayback { get; set; }

        protected virtual void UpdateProperties()
        {
            ItemType = Item?.GetType();
            Title = $"{(Item as TVShowEpisode)?.SeasonName} {(Item as TVShowEpisode)?.EpisodeNo}".Trim();
            if (string.IsNullOrWhiteSpace(Title))
                Title = Item?.Name ?? string.Empty;
            Subtitle = (Item as TVShowEpisode)?.ShowName ?? string.Empty;
            Path = (Item as MediaItem)?.Path ?? string.Empty;
            HasDownload = (Item as MediaItem)?.HasDownload ?? false;
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

        protected abstract void ExecuteStartPlayback();

        protected abstract bool CanStartPlayback();

    }
}
