using Mediathek.Models.Overview;
using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Mediathek.ViewModels.MediaLists.MediaListItem;
using System;
using System.ComponentModel;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists
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
            var vm = Items.FirstOrDefault(vm => vm.HasItem(show));
            if (vm != null)
                Items.Remove(vm);
        }

        protected override void ProcessTVShowCollectionRemoved(TVShowCollection collection)
        {
            var vm = Items.FirstOrDefault(item => item.HasItem(collection));
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
            if (Items.Any(item =>
                          (item.Item is not null) && (item.Item.GetType() == mediaItem.GetType())
                && (item.Item.Id == mediaItem.Id)))
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
            else if (mediaItem is TVShowCollection)
            {
                vm = new TVShowCollectionListItemViewModel(mediaItem as TVShowCollection,
                                                           StatusPublisher,
                                                           NavigationManager,
                                                           Settings,
                                                           DownloadManager,
                                                           MediaLibrary);
            }
            else if (mediaItem is OverviewElement)
            {
                vm = new OverviewElementListItemViewModel(mediaItem as OverviewElement,
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
            if (ParentCollection is null)
                LoadTVShows2(0);
            else
                LoadTVShows(0);
        }

        private List<TVShowCollection> loadingCollections = new List<TVShowCollection>();

        private async void LoadTVShows2(int offset)
        {
            var found = 0;
            var shows = (await MediaLibrary.GetOverviewElements(offset, 10, nameof(TVShow), nameof(TVShowCollection)))
                    .OrderBy(show => show.Year)
                    .ThenBy(show => show.Name);
            foreach (var show in shows)
            {
                found++;
                Add(show);
            }

            if (found > 0)
                LoadTVShows2(offset + found);
        }

        private async void LoadTVShows(int offset)
        {
            var found = 0;
            if ((offset == 0) && (ParentCollection is null))
            {
                loadingCollections.Clear();
                loadingCollections.AddRange((await MediaLibrary.GetTVShowCollections()).ToArray());
            }
            var shows = (ParentCollection is null) ? 
                (await MediaLibrary.GetTVShows(offset, 10)) : 
                (await MediaLibrary.GetTVShows(ParentCollection.Id))
                    .OrderBy(show => show.PremieredAt)
                    .ThenBy(show => show.Name);
            foreach (var show in shows)
            {
                found += 1;
                if (show.CollectionId == 0)
                    Add(show);
                else if (ParentCollection is not null)
                    Add(show);
                else
                {
                    var collection = loadingCollections.FirstOrDefault(c => c.Id == show.CollectionId);
                    if (collection is not null)
                    {
                        if (collection.Picture is null)
                            collection.Picture = show.Picture;
                        Add(collection);
                        loadingCollections.Remove(collection);
                    }
                }
            }
            if ((found > 0) && (ParentCollection is null))
                LoadTVShows(offset + found);
            else if (found == 0)
                foreach (var collection in loadingCollections)
                    Add(collection);
        }

        public void SetParent(TVShowCollection collection, TVShow show, TVShowSeason season)
        {
            ParentCollection = collection;
            ParentShow = show;
            ParentSeason = season;
        }

        public TVShowCollection ParentCollection { get; set; }

        public TVShow ParentShow { get; set; }

        public TVShowSeason ParentSeason { get; set; }

        protected override void ExecuteAddCollection()
        {
            var existing = Items
                .Where(vm => vm is TVShowCollectionListItemViewModel)
                .FirstOrDefault(vm => vm.Item.Id == 0);
            if (existing is not null)
                return;

            var collection = new TVShowCollection();
            BaseMediaListItemViewModel vm = new TVShowCollectionListItemViewModel(collection,
                                                                                  StatusPublisher,
                                                                                  NavigationManager,
                                                                                  Settings,
                                                                                  DownloadManager,
                                                                                  MediaLibrary);
            vm.PropertyChanged += NewItemPropertyChanged;
            Items.Insert(0, vm);
        }

        private void NewItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            BaseMediaListItemViewModel vm = sender as BaseMediaListItemViewModel;
            switch (e.PropertyName)
            {
                case nameof(BaseMediaListItemViewModel.Item):
                    if (vm.Item is null)
                    {
                        Items.Remove(vm);
                        vm.PropertyChanged -= NewItemPropertyChanged;
                    }
                    break;
                case nameof(BaseMediaListItemViewModel.IsStored):
                    if (vm.IsStored)
                        vm.PropertyChanged -= NewItemPropertyChanged;
                    break;
            }
        }

    }
}
