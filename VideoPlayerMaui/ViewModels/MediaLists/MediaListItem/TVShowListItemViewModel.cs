using System;
using System.Linq;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class TVShowListItemViewModel: MediaListItemViewModel
    {

        public TVShowListItemViewModel(
            TVShow show,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager)
            : base(show, statusPublisher, navigationManager) { }

        public override void OpenDetails()
        {
            NavigationManager.OpenTVShow(Item as TVShow);
        }

    }
}
