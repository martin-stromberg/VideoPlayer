using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class TVShowListViewModel: BaseMediaListViewModel
    {

        public TVShowListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager, mediaLibrary) { }

        public override void OnAppeared()
        {
            base.OnAppeared();
            if (ParentSeason != null)
                LoadEpisodes(ParentSeason);
            else if (ParentShow != null)
                LoadSeasons(ParentShow);
            else
                LoadTVShows();
        }

        private void Add(BaseModel mediaItem)
        {
            if (Items.Any(item => item.Item.Id == mediaItem.Id))
                return;
            MediaListItemViewModel vm;
            if (mediaItem is TVShow)
                vm = new TVShowListItemViewModel(mediaItem as TVShow, StatusPublisher, NavigationManager);
            else if (mediaItem is TVShowSeason)
                vm = new TVShowSeasonListItemViewModel(mediaItem as TVShowSeason, StatusPublisher, NavigationManager);
            else if (mediaItem is TVShowEpisode)
                vm = new TVShowEpisodeListItemViewModel(mediaItem as TVShowEpisode, StatusPublisher, NavigationManager);
            else
                return;
            Items.Add(vm);
        }

        private async void LoadEpisodes(TVShowSeason parentSeason)
        {
            var episodes = await MediaLibrary.GetTVShowEpisodes(parentSeason.Id);
            foreach (var episode in episodes.OrderBy(entry => entry.EpisodeNo))
                Add(episode);
        }

        private async void LoadSeasons(TVShow parentShow)
        {
            var seasons = await MediaLibrary.GetTVShowSeasons(ParentShow.Id);
            if (seasons.Count() == 1)
            {
                ParentSeason = seasons.First();
                LoadEpisodes(ParentSeason);
            }
            else
                foreach (var season in seasons.OrderBy(entry => entry.Name))
                    Add(season);
        }

        private async void LoadTVShows()
        {
            var shows = await MediaLibrary.GetTVShows();
            foreach (var show in shows.OrderBy(entry => entry.Name))
                Add(show);
        }

        public void SetParent(TVShow show, TVShowSeason season)
        {
            ParentShow = show;
            ParentSeason = season;
        }

        public TVShow ParentShow { get; set; }

        public TVShowSeason ParentSeason { get; set; }

    }
}
