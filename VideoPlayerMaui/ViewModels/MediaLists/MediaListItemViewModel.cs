using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class MediaListItemViewModel: BaseViewModel
    {

        public MediaListItemViewModel(
            BaseModel mediaItem,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager)
            : base(statusPublisher, navigationManager)
        {
            Item = mediaItem;
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

    }
}
