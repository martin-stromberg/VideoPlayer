using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.MediaLibrary.Scanner;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists.Details
{
    public class BaseDetailsViewModel: BaseViewModel
    {

        private readonly IDownloadManager downloadManager;
        private readonly IMediaLibrary mediaLibrary;
        private readonly ILibraryScanner _LibraryScanner;

        public Command DeleteCollection { get; }

        public Command RescanCollection { get; }

        protected BaseModel CurrentMediaCollection { get; set; }

        protected IMediaLibrary MediaLibrary
        {
            get
            {
                return mediaLibrary;
            }
        }

        protected IDownloadManager DownloadManager
        {
            get
            {
                return downloadManager;
            }
        }

        public BaseDetailsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settings,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary,
            ILibraryScanner libraryScanner)
            : base(statusPublisher, navigationManager, settings)
        {
            _LibraryScanner = libraryScanner;
            this.downloadManager = downloadManager;
            this.mediaLibrary = mediaLibrary;

            DeleteCollection = new Command(() => ExecuteDeleteCollection());
            RescanCollection = new Command(() => ExecuteRescanCollection());
        }

        protected virtual void ExecuteRescanCollection()
        {
            _LibraryScanner.Rescan(CurrentMediaCollection as TVShow);
        }

        protected virtual void ExecuteDeleteCollection()
        {
            if (CurrentMediaCollection is TVShow)
                mediaLibrary.RemoveTVShowAsync(CurrentMediaCollection as TVShow);
            else if (CurrentMediaCollection is TVShowSeason)
                mediaLibrary.RemoveTVShowSeasonAsync(CurrentMediaCollection as TVShowSeason);
            else if (CurrentMediaCollection is Movie)
                mediaLibrary.RemoveMovieAsync(CurrentMediaCollection as Movie);
            else if (CurrentMediaCollection is MovieCollection)
                mediaLibrary.RemoveMovieCollectionAsync(CurrentMediaCollection as MovieCollection);
            else if (CurrentMediaCollection is TVShowCollection)
                mediaLibrary.RemoveTVShowCollectionAsync(CurrentMediaCollection as TVShowCollection);
            NavigationManager.NavigateBack();
        }

        protected async Task<IEnumerable<DownloadSession>> StartDownload(BaseModel item)
        {
            return await downloadManager.StartDownloadAsync(item, MediaItemCopyType.Download);
        }

    }
}
