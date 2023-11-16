using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.Movies;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class MovieListItemViewModel: BaseMediaListItemViewModel
    {

        private readonly Func<IEnumerable<BaseModel>> _GetCollectionElements;

        public MovieListItemViewModel(
            Movie movie,
            Func<IEnumerable<BaseModel>> GetCollectionElements,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService)
            : base(movie, statusPublisher, navigationManager, settingsService)
        {
            _GetCollectionElements = GetCollectionElements;
        }

        public override async void OpenDetails()
        {
            await NavigationManager.OpenMovie(Item as Movie, _GetCollectionElements);
        }

        protected override bool CanStartPlayback()
        {
            return true;
        }

        protected override async void ExecuteStartPlayback()
        {
            OpenDetails();
        }

    }
}
