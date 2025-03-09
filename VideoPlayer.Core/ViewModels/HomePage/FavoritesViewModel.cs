using Microsoft.Extensions.Logging;
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
            IResourceManager resourceManager,
            ILogger<FavoritesViewModel> logger) 
            : base(playlistManager.Favorites, navigationManager, resourceManager, logger)
        {
            Title = "Favoriten";
            AllowAutoPlay = false;
        }
    }
}
