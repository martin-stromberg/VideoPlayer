using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Playlists;
using VideoPlayer.ViewModels.MediaOverview;

namespace VideoPlayer.ViewModels.HomePage
{
    public class HomePageViewModel: BaseHomePageViewModel, IMultiEventCollection
    {
        public HomePageViewModel(
            INavigationManager navigationManager,
            IPlaylistManager playlistManager)
            : base()
        {
            Title = "Videoplayer";            
            this.navigationManager = navigationManager;
            NextPlayingContext = new NextPlayingViewModel(playlistManager, navigationManager);
            NewContext = new NewPlaylistViewModel(playlistManager, navigationManager);
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
