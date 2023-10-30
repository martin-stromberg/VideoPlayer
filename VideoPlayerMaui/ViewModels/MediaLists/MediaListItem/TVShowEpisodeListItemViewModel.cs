using VideoPlayer.Models;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class TVShowEpisodeListItemViewModel: MediaListItemViewModel
    {

        private readonly Func<IEnumerable<BaseModel>> _GetCollectionElements;

        public TVShowEpisodeListItemViewModel(
            TVShowEpisode episode,
            Func<IEnumerable<BaseModel>> GetCollectionElements,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService)
            : base(episode, statusPublisher, navigationManager, settingsService)
        {
            _GetCollectionElements = GetCollectionElements;
        }

        public override async void OpenDetails()
        {
            await NavigationManager.OpenTVShowEpisodeAsync(Item as TVShowEpisode, _GetCollectionElements);
        }

        protected override bool CanStartPlayback()
        {
            return true;
        }

        protected override void ExecuteStartPlayback()
        {
            OpenDetails();
        }

    }
}
