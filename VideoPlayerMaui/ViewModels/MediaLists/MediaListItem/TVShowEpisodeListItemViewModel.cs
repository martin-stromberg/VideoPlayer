using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class TVShowEpisodeListItemViewModel: MediaListItemViewModel
    {

        public TVShowEpisodeListItemViewModel(
            TVShowEpisode episode,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager)
            : base(episode, statusPublisher, navigationManager) { }

        public override void OpenDetails()
        {
            NavigationManager.OpenTVShowEpisode(Item as TVShowEpisode);
        }

    }
}
