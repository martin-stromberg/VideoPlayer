using MyVideoPlayer.ViewModels.Menu;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation
{
    public class NavigationContentViewModel: BaseViewModel
    {

        protected IServiceProvider ServiceProvider { get; }

        protected IMediaLibrary MediaLibrary { get; }

        protected NavigationContentViewModel CreateViewModel(Type viewModelType, params object[] args)
        {
            args = args.Concat(new object[] { MediaLibrary, ServiceProvider }).ToArray();
            return Activator.CreateInstance(viewModelType, args) as NavigationContentViewModel;
        }

        public NavigationContentViewModel(IMediaLibrary mediaLibrary, IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            MediaLibrary = mediaLibrary;
            if (MediaLibrary != null)
            {
                MediaLibrary.ModelElementAdded += MediaLibrary_ModelElementAdded;
                MediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdated;
                MediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;
            }
            Items.CollectionChanged += Items_CollectionChanged;
        }

        protected virtual void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e) { }

        protected virtual void MediaLibrary_ModelElementUpdated(object sender, BaseModelEventArgs e) { }

        protected virtual void MediaLibrary_ModelElementAdded(object sender, BaseModelEventArgs e) { }

        public ObservableCollection<BaseMediaElementBoxViewModel> Items { get; set; } = new ObservableCollection<BaseMediaElementBoxViewModel>();

        private List<BaseMediaElementBoxViewModel> BackgroundItems { get; } = new List<BaseMediaElementBoxViewModel>();

        public virtual MenuViewModel MenuViewModel { get; }

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (var item in e.OldItems.OfType<BaseMediaElementBoxViewModel>())
                {
                    item.Tapped -= Item_Tapped;
                    item.DownloadRequested -= Item_DownloadRequested;
                }
            if (e.NewItems != null)
                foreach (var item in e.NewItems.OfType<BaseMediaElementBoxViewModel>())
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

        protected virtual void OnNavigationRequest(ViewModelEventArgs e)
        {
            NavigationRequested?.Invoke(this, e);
        }

        protected virtual void OnDeleteRequest(BaseModelEventArgs e)
        {
            ItemDeleteRequested?.Invoke(this, e);
        }

        protected virtual void OnResetScan(BaseModelEventArgs e)
        {
            ResetScanRequested?.Invoke(this, e);
        }

        public event EventHandler<BaseModelEventArgs> ItemDeleteRequested;

        public event EventHandler<BaseModelEventArgs> ResetScanRequested;

        public event EventHandler<ViewModelEventArgs> NavigationRequested;

        public event EventHandler<ViewModelEventArgs> CloseRequested;

        protected virtual void OnCloseRequested()
        {
            CloseRequested?.Invoke(this, new ViewModelEventArgs(this));
        }

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

        public virtual void OnAppeared() { }

        public virtual void OnDisappeared() { }

        public async Task ReadAllSourcesAsync()
        {
            foreach (var source in await MediaLibrary.GetSourcesAsync())
                MediaLibrary_ModelElementAdded(this, new BaseModelEventArgs(source));
        }

        internal virtual async Task ReadMediaCollection(MediaSource source)
        {
            while (LoadingDataWaiting)
                await Task.Delay(10);
            foreach (var collection in (await MediaLibrary.GetMediaItemCollectionsAsync(source.Id)).OrderBy(c =>
                                                                                                            c.ParentCollectionId))
                MediaLibrary_ModelElementAdded(this, new BaseModelEventArgs(collection));
        }

        internal virtual async Task ReadMediaItems(MediaItemCollection collection)
        {
            while (LoadingDataWaiting)
                await Task.Delay(10);
            foreach (var coll in (await MediaLibrary.GetMediaItemCollectionsAsync(collection.MediaSourceId)).OrderBy(c =>
                                                                                                                     c.ParentCollectionId))
                MediaLibrary_ModelElementAdded(this, new BaseModelEventArgs(coll));
            foreach (var mediaItem in (await MediaLibrary.GetMediaItemsAsync(collection.Id)).OrderBy(c =>
                                                                                                     c.ParentCollectionId))
                MediaLibrary_ModelElementAdded(this, new BaseModelEventArgs(mediaItem));
        }

        internal virtual void PutBesideItems()
        {
            BackgroundItems.AddRange(Items);
            Items.Clear();
        }

        protected bool LoadingDataWaiting = false;

        internal virtual async Task ReloadBackgroundItems()
        {
            LoadingDataWaiting = true;
            try
            {
                await Task.Delay(200);
                await Task.Run(() =>
                {
                    foreach (var item in BackgroundItems)
                        MainThread.InvokeOnMainThreadAsync(() => { Items.Add(item); });
                    BackgroundItems.Clear();
                });
            }
            finally
            {
                LoadingDataWaiting = false;
            }
        }

    }
}
