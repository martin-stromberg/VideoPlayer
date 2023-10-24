using System;
using System.Linq;
using VideoPlayer.Models.Movies;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class MovieListItemViewModel: MediaListItemViewModel
    {

        public MovieListItemViewModel(
            Movie movie,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager)
            : base(movie, statusPublisher, navigationManager) { }

        public override void OpenDetails()
        {
            NavigationManager.OpenMovie(Item as Movie);
        }

    }
}
