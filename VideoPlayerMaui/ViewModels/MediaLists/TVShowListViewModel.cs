using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class TVShowListViewModel: BaseMediaListViewModel
    {

        public TVShowListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager, mediaLibrary) { }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LoadTVShows();
        }

        private void Add(BaseModel mediaItem)
        {
            if (Items.Any(item => item.Item.Id == mediaItem.Id))
                return;
            var vm = new MediaListItemViewModel(mediaItem, StatusPublisher, NavigationManager);
            Items.Add(vm);
        }

        private async void LoadTVShows()
        {
            var shows = await MediaLibrary.GetTVShows();
            foreach (var show in shows.OrderBy(entry => entry.Name))
                Add(show);
        }

    }
}
