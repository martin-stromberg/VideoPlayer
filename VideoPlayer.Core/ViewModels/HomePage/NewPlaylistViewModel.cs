using VideoPlayer.Navigation;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;

namespace VideoPlayer.ViewModels.HomePage
{
    public class NewPlaylistViewModel: BasePlayingViewModel
    {
        
        public NewPlaylistViewModel(
            IPlaylistManager playlistManager,
            INavigationManager navigationManager, IResourceManager resourceManager)
            :base(playlistManager.NewPlaylist, navigationManager, resourceManager)
        {
            Title = "Neu hinzugefügt";
            AllowAutoPlay = false;
        }
    }
}
