using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public abstract class MediaListItemViewModel: BaseViewModel
    {

        public MediaListItemViewModel(
            BaseModel mediaItem,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService)
            : base(statusPublisher, navigationManager, settingsService)
        {
            Item = mediaItem;
            StartPlayback = new Command(() => ExecuteStartPlayback(), () => CanStartPlayback());
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

        public Command StartPlayback { get; set; }

        private void UpdateProperties()
        {
            ItemType = Item?.GetType();
            Title = Item?.Name ?? string.Empty;
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
