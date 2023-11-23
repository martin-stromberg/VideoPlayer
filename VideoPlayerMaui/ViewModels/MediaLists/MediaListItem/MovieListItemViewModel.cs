using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.Movies;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
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
            ISettingsService settingsService,
            IMediaDownloader mediaDownloader,
            IMediaLibrary mediaLibrary)
            : base(movie, statusPublisher, navigationManager, settingsService, mediaDownloader, mediaLibrary)
        {
            _GetCollectionElements = GetCollectionElements;
        }

        public override async void OpenDetails()
        {
            await NavigationManager.OpenMovie(Item as Movie, _GetCollectionElements);
        }

        public override async void OpenCategory()
        {
            if ((Item as Movie).CollectionId == 0)
                await NavigationManager.OpenMovie(Item as Movie, _GetCollectionElements);
            else
            {
                var collection = await MediaLibrary.GetMovieCollection((Item as Movie).CollectionId);
                NavigationManager.OpenMovieCollection(collection);
            }
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
