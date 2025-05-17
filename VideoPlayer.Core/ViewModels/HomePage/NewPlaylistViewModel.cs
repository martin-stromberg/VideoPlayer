using Microsoft.Extensions.Logging;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library.Tenants;
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
            ITenantSelection tenantSelection,
            ILogger<NewPlaylistViewModel> logger)
            :base(playlistManager.NewPlaylist, tenantSelection, navigationManager, resourceManager,logger)
        {
            Title = "Neu hinzugefügt";
            AllowAutoPlay = false;
        }
    }
}
