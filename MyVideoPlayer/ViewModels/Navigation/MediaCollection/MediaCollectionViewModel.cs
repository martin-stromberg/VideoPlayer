using MyVideoPlayer.ViewModels.Menu;
using MyVideoPlayer.ViewModels.Navigation.Sources;
using System;
using System.ComponentModel;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.MediaCollection
{
    public class MediaCollectionViewModel: NavigationContentViewModel
    {

        public MediaCollectionViewModel(
            IMediaLibrary mediaLibrary,
            IServiceProvider serviceProvider)
            : base(mediaLibrary, serviceProvider) { }

        public MediaSource Source { get; internal set; }

        public MediaItemCollection Collection { get; internal set; }

        private MenuViewModel menuViewModel = null;

        public override MenuViewModel MenuViewModel
        {
            get
            {
                if (menuViewModel == null)
                    if (Collection == null)
                        SetMenuViewModel(new SourceMenuViewModel());
                    else
                        SetMenuViewModel(new MediaCollectionMenuViewModel());
                return menuViewModel;
            }
        }

        protected void SetMenuViewModel(MenuViewModel vm)
        {
            if (menuViewModel != null)
                menuViewModel.CommandExecuted -= MenuViewModel_CommandExecuted;
            menuViewModel = vm;
            if (menuViewModel != null)
                menuViewModel.CommandExecuted += MenuViewModel_CommandExecuted;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(MenuViewModel)));
        }

        protected virtual void MenuViewModel_CommandExecuted(object sender, MenuActionEventArgs e)
        {
            switch (e.Action.CommandParameter)
            {
                case SourceMenuViewModel.CommandName_ConfigSource:
                    OnNavigationRequest(new ViewModelEventArgs(CreateViewModel(typeof(SourceConfigurationViewModel), Source)));
                    break;
                case SourceMenuViewModel.CommandName_Remove:
                    OnDeleteRequest(new BaseModelEventArgs(Source));
                    break;
                case SourceMenuViewModel.CommandName_Rescan:
                    OnResetScan(new BaseModelEventArgs(Source));
                    break;
                case MediaCollectionMenuViewModel.CommandName_Rescan:
                    OnResetScan(new BaseModelEventArgs(Collection));
                    break;
            }
        }

        protected override void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            base.MediaLibrary_ModelElementRemoved(sender, e);
            if ((e.Element is MediaSource) && (((MediaSource)e.Element).Id == (Source?.Id)))
                OnNavigationRequest(new ViewModelEventArgs(this));
        }

        protected override void MediaLibrary_ModelElementAdded(object sender, BaseModelEventArgs e)
        {
            base.MediaLibrary_ModelElementAdded(sender, e);
            var currCollection = e.Element as MediaItemCollection;
            if (currCollection != null)
            {
                if ((Collection == null) && (currCollection.ParentCollectionId == 0))
                    Collection = currCollection;
                else if ((Collection != null) && (currCollection != null)
                    && (Collection.Id == currCollection.ParentCollectionId))
                    AddMediaCollection(currCollection);
            }
            var currItem = e.Element as MediaItem;
            if (currItem != null)
            {
                AddMediaItem(currItem);
            }
        }

        private void AddMediaItem(MediaItem currItem)
        {
            if (currItem.ParentCollectionId != (Collection?.Id))
                return;
            if (Items
                .Where(item => item is MediaItemBoxViewModel)
                .Cast<MediaItemBoxViewModel>()
                .Any(existing => existing.Item.Id == currItem.Id))
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var vm = ServiceProvider.GetService<MediaItemBoxViewModel>();

                vm.Title = currItem.Name;
                vm.Item = currItem;
                vm.Source = Source;
                vm.Collection = Collection;
                Items.Add(vm);
            });
        }

        private void AddMediaCollection(MediaItemCollection currCollection)
        {
            if (currCollection.ParentCollectionId != (Collection?.Id))
                return;
            if (Items
                .Where(item => item is MediaCollectionBoxViewModel)
                .Cast<MediaCollectionBoxViewModel>()
                .Any(existing => existing.Collection.Id == currCollection.Id))
                return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var vm = ServiceProvider.GetService<MediaCollectionBoxViewModel>();
                vm.Title = currCollection.Name;
                vm.Source = Source;
                vm.Collection = currCollection;
                vm.ParentCollection = Collection;
                Items.Add(vm);
            });
        }

        public override async void OnAppeared()
        {
            base.OnAppeared();
            if (Collection == null)
                await ReadMediaCollection(Source);
            if (Collection != null)
                await ReadMediaItems(Collection);
        }

    }
}
