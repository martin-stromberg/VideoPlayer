using Microsoft.Extensions.Logging;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;

namespace VideoPlayer.ViewModels.HomePage
{
    public class NewPlaylistViewModel: BasePlayingViewModel
    {
        
        public NewPlaylistViewModel(
            IPlaylistManager playlistManager,
            INavigationManager navigationManager, 
            IResourceManager resourceManager,
            ILogger<NewPlaylistViewModel> logger)
            :base(playlistManager.NewPlaylist, navigationManager, resourceManager,logger)
        {
            Title = "Neu hinzugefügt";
            AllowAutoPlay = false;
        }
    }
}
