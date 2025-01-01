using VideoPlayer.Navigation;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;

namespace VideoPlayer.ViewModels.HomePage
{
    public class FavoritesViewModel : BasePlayingViewModel
    {
        public FavoritesViewModel(
            IPlaylistManager playlistManager,
            INavigationManager navigationManager, 
            IResourceManager resourceManager) 
            : base(playlistManager.Favorites, navigationManager, resourceManager)
        {
            Title = "Favoriten";
            AllowAutoPlay = false;
        }
    }
}
