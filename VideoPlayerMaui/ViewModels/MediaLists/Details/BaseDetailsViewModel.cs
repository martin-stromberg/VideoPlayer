using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Models;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.Details
{
    public class BaseDetailsViewModel : BaseViewModel
    {
        private readonly IDownloadManager downloadManager;
        private readonly IMediaLibrary mediaLibrary;

        public Command DeleteCollection { get; }
        protected BaseModel Collection { get; set; }
        protected IMediaLibrary MediaLibrary { get => mediaLibrary; }
        protected IDownloadManager DownloadManager { get => downloadManager; }

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
            return await downloadManager.StartDownloadAsync(item, Models.MediaItems.MediaItemCopyType.Download);
        }
    }
}
