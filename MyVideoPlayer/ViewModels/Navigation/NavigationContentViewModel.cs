using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation
{
    public class NavigationContentViewModel: BaseViewModel
    {
        protected IServiceProvider ServiceProvider { get; }

        private IMediaLibrary mediaLibrary;

        public NavigationContentViewModel(IMediaLibrary mediaLibrary, IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            this.mediaLibrary = mediaLibrary;
            this.mediaLibrary.ModelElementAdded += MediaLibrary_ModelElementAdded;
            this.mediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdated;
            this.mediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;
            Items.CollectionChanged += Items_CollectionChanged;
        }
        protected virtual void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {

        }
        protected virtual void MediaLibrary_ModelElementUpdated(object sender, BaseModelEventArgs e)
        {

        }
        protected virtual void MediaLibrary_ModelElementAdded(object sender, BaseModelEventArgs e)
        {
            
        }

        public ObservableCollection<BaseMediaElementBoxViewModel> Items { get; set; } = new ObservableCollection<BaseMediaElementBoxViewModel>();
        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (var item in e.NewItems.Cast<BaseMediaElementBoxViewModel>())
                {
                    item.Tapped -= Item_Tapped;
                    item.DownloadRequested -= Item_DownloadRequested;
                }
            if (e.NewItems != null)
                foreach (var item in e.NewItems.Cast<BaseMediaElementBoxViewModel>())
                {
                    item.Tapped += Item_Tapped;
                    item.DownloadRequested += Item_DownloadRequested;
                }
        }

        private void Item_Tapped(object sender, EventArgs e)
        {
            BaseMediaElementBoxViewModel item = sender as BaseMediaElementBoxViewModel;
            if (!Items.Contains(item))
                return;
            OnItemTapped(new MediaElementBoxViewModelEventArgs(item));
        }
        public event EventHandler<MediaElementBoxViewModelEventArgs> ItemTapped;
        protected virtual void OnItemTapped(MediaElementBoxViewModelEventArgs e)
        {
            ItemTapped?.Invoke(this, e);
        }

        private void Item_DownloadRequested(object sender, EventArgs e)
        {
            BaseMediaElementBoxViewModel item = sender as BaseMediaElementBoxViewModel;
            if (!Items.Contains(item))
                return;
            OnItemDownloadRequested(new MediaElementBoxViewModelEventArgs(item));
        }
        public event EventHandler<MediaElementBoxViewModelEventArgs> ItemDownloadRequested;
        protected virtual void OnItemDownloadRequested(MediaElementBoxViewModelEventArgs e)
        {
            ItemDownloadRequested?.Invoke(this, e);
        }

        public virtual void OnAppeared()
        {
            
        }

        public async Task ReadAllSourcesAsync()
        {
            foreach (var source in await this.mediaLibrary.GetSourcesAsync())
                MediaLibrary_ModelElementAdded(this, new BaseModelEventArgs(source));
        }

        internal async Task ReadMediaCollection(MediaSource source)
        {
            foreach (var collection in (await this.mediaLibrary.GetMediaItemCollectionsAsync(source.Id)).OrderBy(c => c.ParentCollectionId))
                MediaLibrary_ModelElementAdded(this, new BaseModelEventArgs(collection));
        }

        internal async Task ReadMediaItems(MediaItemCollection collection)
        {
            foreach (var coll in (await this.mediaLibrary.GetMediaItemCollectionsAsync(collection.MediaSourceId)).OrderBy(c => c.ParentCollectionId))
                MediaLibrary_ModelElementAdded(this, new BaseModelEventArgs(coll));
            foreach (var mediaItem in (await this.mediaLibrary.GetMediaItemsAsync(collection.Id)).OrderBy(c => c.ParentCollectionId))
                MediaLibrary_ModelElementAdded(this, new BaseModelEventArgs(mediaItem));
        }
    }
}
