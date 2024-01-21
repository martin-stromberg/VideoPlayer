using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;

namespace Mediathek.ViewModels.MediaLists.MediaListItem
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
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(episode, statusPublisher, navigationManager, settingsService, downloadManager, mediaLibrary)
        {
            _GetCollectionElements = GetCollectionElements;
        }

        public override async void OpenDetails()
        {
            try
            {
                await NavigationManager.OpenTVShowEpisodeAsync(Item as TVShowEpisode, _GetCollectionElements);
            }
            catch (Exception ex) { }
        }

        public override async void OpenCategory()
        {
            NavigationManager.OpenTVShow(null, null, Item as TVShowEpisode);
        }

        protected override bool CanStartPlayback()
        {
            return true;
        }

        protected override void ExecuteStartPlayback()
        {
            OpenDetails();
        }

        protected override void ExecuteSaveNewItem()
        {
            throw new NotImplementedException();
        }

        protected override void ExecuteCancelNewItem()
        {
            throw new NotImplementedException();
        }

    }
}
