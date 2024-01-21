using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists.MediaListItem
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
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(movie, statusPublisher, navigationManager, settingsService, downloadManager, mediaLibrary)
        {
            _GetCollectionElements = GetCollectionElements;
        }

        public override async void OpenDetails()
        {
            var args = new BaseModelProcessEventArgs(Item);
            OnBeforeOpenDetails(args);
            if (args.Continue)
                NavigationManager.OpenMovie(Item as Movie, _GetCollectionElements);
        }

        public override async void OpenCategory()
        {
            var args = new BaseModelProcessEventArgs(Item);
            OnBeforeOpenDetails(args);
            if (!args.Continue)
                return;

            if ((Item as Movie).CollectionId == 0)
                NavigationManager.OpenMovie(Item as Movie, _GetCollectionElements);
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
