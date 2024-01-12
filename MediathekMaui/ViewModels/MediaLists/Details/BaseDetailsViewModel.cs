using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
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

        public Command DeleteCollection { get; }

        protected BaseModel Collection { get; set; }

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
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager, settings)
        {
            this.downloadManager = downloadManager;
            this.mediaLibrary = mediaLibrary;

            DeleteCollection = new Command(() => ExecuteDeleteCollection());
        }

        protected virtual void ExecuteDeleteCollection()
        {
            if (Collection is TVShow)
                mediaLibrary.RemoveTVShowAsync(Collection as TVShow);
            else if (Collection is TVShowSeason)
                mediaLibrary.RemoveTVShowSeasonAsync(Collection as TVShowSeason);
            else if (Collection is Movie)
                mediaLibrary.RemoveMovieAsync(Collection as Movie);
            else if (Collection is MovieCollection)
                mediaLibrary.RemoveMovieCollectionAsync(Collection as MovieCollection);
            NavigationManager.NavigateBack();
        }

        protected async Task<IEnumerable<DownloadSession>> StartDownload(BaseModel item)
        {
            return await downloadManager.StartDownloadAsync(item, MediaItemCopyType.Download);
        }

    }
}
