using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.Details
{
    public class MovieCollectionViewModel : BaseMediaListViewModel
    {
        public MovieCollectionViewModel(
            IStatusPublisher statusPublisher, 
            INavigationManager navigationManager, 
            IMediaLibrary mediaLibrary, 
            IPlaylistManager playlistManager, 
            ISettingsService settingsService, 
            IDownloadManager downloadManager) 
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager, settingsService, downloadManager)
        {
        }
    }
}
