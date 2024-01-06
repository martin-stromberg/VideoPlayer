using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class TVShowListViewModel: BaseMediaListViewModel
    {

        public TVShowListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager)
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager, settingsService, downloadManager) { }

        protected override void ProcessTVShowRemoved(TVShow show)
        {
            var vm = Items.FirstOrDefault(item => item.Item.Id == show.Id);
            if (vm != null)
                Items.Remove(vm);
        }
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
            BaseMediaListItemViewModel vm;
            if (mediaItem is TVShow)
            {
                vm = new TVShowListItemViewModel(mediaItem as TVShow,
                                                 StatusPublisher,
                                                 NavigationManager,
                                                 PlaylistManager,
                                                 Settings,
                                                 DownloadManager,
                                                 MediaLibrary);
            }
            else if (mediaItem is TVShowSeason)
                vm = new TVShowSeasonListItemViewModel(mediaItem as TVShowSeason,
                                                       StatusPublisher,
                                                       NavigationManager,
                                                       PlaylistManager,
                                                       Settings,
                                                       DownloadManager,
                                                       MediaLibrary);
            else if (mediaItem is TVShowEpisode)
            {
                Func<IEnumerable<TVShowEpisode>> getEpisodes = (ParentSeason != null) ? () =>
                                                                                        Items.Select(i => i.Item)
                                                                                             .OfType<TVShowEpisode>() : (new Func<IEnumerable<TVShowEpisode>>(() =>
                                                                                                                                                              new TVShowEpisode[0]));
                vm = new TVShowEpisodeListItemViewModel(mediaItem as TVShowEpisode,
                                                        getEpisodes,
                                                        StatusPublisher,
                                                        NavigationManager,
                                                        Settings,
                                                        DownloadManager,
                                                        MediaLibrary);
            }
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

        private void LoadTVShows()
        {
            LoadTVShows(0);
        }

        private async void LoadTVShows(int offset)
        {
            var found = 0;
            var shows = await MediaLibrary.GetTVShows(offset, 10);
            foreach (var show in shows)
            {
                found += 1;
                Add(show);
            }
            if (found > 0)
                LoadTVShows(offset + found);
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
