using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class TVShowSeasonListItemViewModel: MediaListItemViewModel
    {

        public TVShowSeasonListItemViewModel(
            TVShowSeason season,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager)
            : base(season, statusPublisher, navigationManager) { }

        public override void OpenDetails()
        {
            NavigationManager.OpenTVShowSeason(Item as TVShowSeason);
        }

    }
}
