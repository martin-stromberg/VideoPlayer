using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.MediaCollection
{
    public class MediaCollectionViewModel : NavigationContentViewModel
    {
        public MediaCollectionViewModel(
            IMediaLibrary mediaLibrary,
            IServiceProvider serviceProvider) 
            : base(mediaLibrary, serviceProvider)
        {
        }

        public MediaSource Source { get; internal set; }
        public MediaItemCollection Collection { get; internal set; }

        protected override void MediaLibrary_ModelElementAdded(object sender, BaseModelEventArgs e)
        {
            base.MediaLibrary_ModelElementAdded(sender, e);
            var currCollection = e.Element as MediaItemCollection;
            if (currCollection != null)
            {
                if (Collection == null && currCollection.ParentCollectionId == 0)
                    Collection = currCollection;
                else if (Collection.Id == currCollection.ParentCollectionId)
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
            if (currItem.ParentCollectionId != Collection?.Id)
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
                vm.Source = this.Source;
                vm.Collection = Collection;
                Items.Add(vm);
            });
        }

        private void AddMediaCollection(MediaItemCollection currCollection)
        {
            if (currCollection.ParentCollectionId != Collection?.Id)
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
                vm.Source = this.Source;
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
