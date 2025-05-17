using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Scanner;
using VideoPlayer.Service.Library.Tenants;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview;

namespace VideoPlayer.ViewModels.HomePage
{
    public class HomePageViewModel: BaseHomePageViewModel, IMultiEventCollection
    {
        private ITenantSelection tenantSelection;
        public HomePageViewModel(
            INavigationManager navigationManager,
            IPlaylistManager playlistManager, 
            IResourceManager resourceManager,
            ILibraryScanner libraryScanner,
            ITenantSelection tenantSelection,
            IProcessorCollection processorCollection,
            ILogger<HomePageViewModel> logger,
            ILogger<NextPlayingViewModel> loggerNextPlaying,
            ILogger<FavoritesViewModel> loggerFavorites,
            ILogger<NewPlaylistViewModel> loggerNewPlaying)
            : base(processorCollection, libraryScanner, logger)
        {
            Title = "Videoplayer";            
            this.navigationManager = navigationManager;
            this.tenantSelection = tenantSelection;
            tenantSelection.TenantChanged += TenantSelection_TenantChanged;
            NextPlayingContext = new NextPlayingViewModel(playlistManager, navigationManager, resourceManager, tenantSelection, loggerNextPlaying);
            FavoritesContext = new FavoritesViewModel(playlistManager, navigationManager, resourceManager, tenantSelection, loggerFavorites);
            NewContext = new NewPlaylistViewModel(playlistManager, navigationManager, resourceManager, tenantSelection, loggerNewPlaying);
        }

        

        public override void ExecuteAppeared()
        {
            base.ExecuteAppeared();
            NextPlayingContext.ExecuteAppeared();
        }
        public override void ExecuteDisappeared()
        {
            NextPlayingContext.ExecuteDisappeared();
            base.ExecuteDisappeared();            
        }

        protected override void ExecuteFirstAppeared()
        {
            base.ExecuteFirstAppeared();
            ExecuteNavigate("home");
        }

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            base.OnPropertyChanged(propertyName);
            switch(propertyName)
            {
                case nameof(SelectedTenant):
                    tenantSelection.ChangeTenant(SelectedTenant);
                    break;
            }
        }

        #region General Navigation
        private readonly INavigationManager navigationManager;

        protected override void ExecuteNavigate(string navigationCategory)
        {
            try
            {
                switch (navigationCategory)
                {
                    case "movies":
                        navigationManager.OpenMovies();
                        break;
                    case "tv":
                        navigationManager.OpenTVShows();
                        break;
                    case "home":
                        break;
                    case "actors":
                        navigationManager.OpenActorsOverview();
                        break;
                    default:
                        throw new NotImplementedException(nameof(navigationCategory));
                }
            }
            catch(Exception ex)
            {
                OnStatusReceived(ex.Message);
            }
        }
        #endregion
        #region IMultiEventCollection
        public IEnumerable<IEventSubscriber> GetSubscribers()
        {
            return new IEventSubscriber[] { NextPlayingContext };
        }

        public IEnumerable<IEventPublisher> GetPublishers()
        {
            return new IEventPublisher[0] { };
        }
        #endregion
        protected override void LoadTenants()
        {
            base.LoadTenants();
            Tenants = tenantSelection.AllTenants;
            SelectedTenant = tenantSelection.CurrentTenant;
        }
        private void TenantSelection_TenantChanged(object sender, string currentTenant)
        {
            SelectedTenant = currentTenant;
            tenantSelection.ChangeTenant(currentTenant);
        }
    }
}
