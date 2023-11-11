using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Navigation;
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
            ISettingsService settingsService)
            : base(statusPublisher, navigationManager, settingsService)
        {
            Mode = ItemViewModel.Box;
            Item = mediaItem;
            StartPlayback = new Command(() => ExecuteStartPlayback(), () => CanStartPlayback());
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

        public Command StartPlayback { get; set; }

        protected virtual void UpdateProperties()
        {
            ItemType = Item?.GetType();
            Title = Item?.Name ?? string.Empty;
            Path = (Item as MediaItem)?.Path ?? string.Empty;
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
