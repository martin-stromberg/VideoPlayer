using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.HomePage
{
    public class NextPlayingViewModel : BasePlayingViewModel
    {
        public NextPlayingViewModel(
            IPlaylistManager playlistManager, 
            INavigationManager navigationManager, IResourceManager resourceManager) 
            : base(playlistManager.NextPlaybackPlaylist, navigationManager, resourceManager)
        {
            Title = "Weiterschauen";
            AllowAutoPlay = true;
        }
    }
}
