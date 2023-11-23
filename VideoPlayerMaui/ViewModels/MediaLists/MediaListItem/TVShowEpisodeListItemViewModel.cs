using VideoPlayer.Models;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class TVShowEpisodeListItemViewModel: BaseMediaListItemViewModel
    {

        private readonly Func<IEnumerable<BaseModel>> _GetCollectionElements;

        public TVShowEpisodeListItemViewModel(
            TVShowEpisode episode,
            Func<IEnumerable<BaseModel>> GetCollectionElements,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            IMediaDownloader mediaDownloader,
            IMediaLibrary mediaLibrary)
            : base(episode, statusPublisher, navigationManager, settingsService, mediaDownloader, mediaLibrary)
        {
            _GetCollectionElements = GetCollectionElements;
        }

        public override async void OpenDetails()
        {
            await NavigationManager.OpenTVShowEpisodeAsync(Item as TVShowEpisode, _GetCollectionElements);
        }

        public override async void OpenCategory()
        {
            var season = await MediaLibrary.GetTVShowSeason((Item as TVShowEpisode).SeasonId);
            var show = await MediaLibrary.GetTVShow(season.ShowId);
            NavigationManager.OpenTVShow(show);
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
