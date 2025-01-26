using Microsoft.Maui.Graphics.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;
using VideoPlayer.Views.MediaOverview;
using VideoPlayer.Views.MediaOverview.Cards;
using VideoPlayer.Views.Protocol;

namespace VideoPlayer.Navigation
{
    public class NavigationManager : INavigationManager
    {
        private const string _RouteNameMovieOverview = "movies";
        private const string _RouteNameTVShowOverview = "tvshows";
        private const string _RouteNameActorsOverview = "actors";
        private const string _RouteNameMovieCard = "movie";
        private const string _RouteNameTVShowCard = "tvshow";
        private const string _RouteNameActor = "actor";
        private const string _RouteNameProtocol = "protocol";

        public NavigationManager()
            :base()
        {
            Routing.RegisterRoute(_RouteNameMovieOverview, typeof(MovieOverviewPage));
            Routing.RegisterRoute(_RouteNameTVShowOverview, typeof(TVShowOverviewPage));
            Routing.RegisterRoute(_RouteNameActorsOverview, typeof(ActorsOverviewPage));
            Routing.RegisterRoute(_RouteNameMovieCard, typeof(MovieCardPage));
            Routing.RegisterRoute(_RouteNameTVShowCard, typeof(TVShowCardPage));
            Routing.RegisterRoute(_RouteNameActor, typeof(ActorPage));
            Routing.RegisterRoute(_RouteNameProtocol, typeof(ProtocolPage));
        }
        #region General
        protected async void NavigateToRoute(string route, 
            Dictionary<string, object> args = default)
        {
            try
            {
                if (args == null)
                    await Shell.Current.GoToAsync(route);
                else
                    await Shell.Current.GoToAsync(route, args);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        public void NavigateBack()
        {
            MainThread.InvokeOnMainThreadAsync(() => { Shell.Current.Navigation.RemovePage(Shell.Current.CurrentPage); });
        }
        #endregion
        #region Overview
        public void OpenMovies()
        {
            NavigateToRoute(_RouteNameMovieOverview);
        }

        public void OpenTVShows()
        {
            NavigateToRoute(_RouteNameTVShowOverview);
        }
        public void OpenActorsOverview()
        {
            NavigateToRoute(_RouteNameActorsOverview);
        }
        #endregion

        public void OpenCard(BaseListItem vm, bool autoPlay = false)
        {
            if ((vm is MovieMediaListItem) || (vm is MovieCollectionMediaListItem))
                NavigateToRoute(_RouteNameMovieCard, new Dictionary<string, object>()
                {
                    { "Id", vm.Id }
                });
            else if ((vm is TVShowMediaListItem) || (vm is TVShowEpisodeMediaListItem) || (vm is TVShowSeasonMediaListItem))
                NavigateToRoute(_RouteNameTVShowCard, new Dictionary<string, object>()
                {
                    { "Id", vm.Id },
                    { "AutoPlay", autoPlay }
                });
            else if (vm is RoleListItem)
            {                
                NavigateToRoute(_RouteNameActor, new Dictionary<string, object>()
                {
                    { "Id", ((Role)((RoleListItem)vm).Element).Actor.Id },
                    { "AutoPlay", autoPlay }
                });
            }
            else if (vm is ActorListItem)
            {
                NavigateToRoute(_RouteNameActor, new Dictionary<string, object>()
                {
                    { "Id", vm.Id },
                    { "AutoPlay", autoPlay }
                });
            }
            else
                throw new NotImplementedException(vm.GetType().Name);
        }
        public void OpenProtocol(string elemType, long elemId)
        {
            NavigateToRoute(_RouteNameProtocol, new Dictionary<string, object>()
                {
                    { "Id", elemId },
                    { "Type", elemType }
                });
        }
        public void CloseCurrentPage()
        {
            MainThread.InvokeOnMainThreadAsync(() => { Shell.Current.Navigation.RemovePage(Shell.Current.CurrentPage); });
        }
    }
}
