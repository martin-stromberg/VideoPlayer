using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview;

namespace VideoPlayer.ViewModels.HomePage
{
    public class HomePageViewModel: BaseHomePageViewModel, IMultiEventCollection
    {
        public HomePageViewModel(
            INavigationManager navigationManager,
            IPlaylistManager playlistManager, 
            IResourceManager resourceManager,
            IProcessorCollection processorCollection)
            : base(processorCollection)
        {
            Title = "Videoplayer";            
            this.navigationManager = navigationManager;
            NextPlayingContext = new NextPlayingViewModel(playlistManager, navigationManager, resourceManager);
            FavoritesContext = new FavoritesViewModel(playlistManager, navigationManager, resourceManager);
            NewContext = new NewPlaylistViewModel(playlistManager, navigationManager, resourceManager);
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

    }
}
