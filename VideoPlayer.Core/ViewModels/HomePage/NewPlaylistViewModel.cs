using VideoPlayer.Navigation;
using VideoPlayer.Service.Playlists;

namespace VideoPlayer.ViewModels.HomePage
{
    public class NewPlaylistViewModel: BasePlayingViewModel
    {
        
        public NewPlaylistViewModel(
            IPlaylistManager playlistManager,
            INavigationManager navigationManager)
            :base(playlistManager.NewPlaylist, navigationManager)
        {
            Title = "Neu hinzugefügt";
            AllowAutoPlay = false;
        }
    }
}
