using System;
using System.Linq;
using VideoPlayer.Models.Movies;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class MovieCollectionListItemViewModel: MediaListItemViewModel
    {

        public MovieCollectionListItemViewModel(
            MovieCollection movieCollection,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager)
            : base(movieCollection, statusPublisher, navigationManager) { }

        public override void OpenDetails()
        {
            NavigationManager.OpenMovieCollection(Item as MovieCollection);
        }

    }
}
