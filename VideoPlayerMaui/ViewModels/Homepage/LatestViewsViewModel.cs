using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.PlaybackHistory;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.PlaybackHistory;
using VideoPlayer.Services.Playlists;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.ViewModels.Homepage
{
    public class LatestViewsViewModel: BaseMediaListViewModel
    {

        private readonly IPlaybackHistoryManager _PlaybackHistoryManager;

        public LatestViewsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            IPlaybackHistoryManager playbackHistoryManager)
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager)
        {
            _PlaybackHistoryManager = playbackHistoryManager;
            _PlaybackHistoryManager.CurrentHistory.Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_PlaybackHistoryManager.IsInitialized)
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        AddNewItems(e.NewItems);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveItems(e.OldItems);
                        break;
                    case NotifyCollectionChangedAction.Move:
                        RemoveItems(e.OldItems);
                        AddNewItems(e.NewItems);
                        break;
                }
        }

        private void RemoveItems(IList oldItems)
        {
            foreach (var item in oldItems.Cast<HistoryEntry>().Where(i => i.TypedItem != null))
            {
                MediaListItemViewModel vm = Items.FirstOrDefault(i => i.Item.Id == item.TypedItem.Id);
                if (vm == null)
                    continue;
                Items.Remove(vm);
            }
        }

        private async void AddNewItems(IList newItems)
        {
            foreach (var item in newItems.Cast<HistoryEntry>()
                                         .OrderByDescending(i => i.Id)
                                         .Where(i => i.TypedItem != null))
            {
                MediaListItemViewModel vm;
                if (item.TypedItem is TVShowEpisode)
                {
                    var episode = item.TypedItem as TVShowEpisode;
                    TVShowSeason season = null;
                    TVShow show;
                    vm = new TVShowEpisodeListItemViewModel(episode, () => null, StatusPublisher, NavigationManager);
                    if (vm.Picture == null)
                    {
                        season = await MediaLibrary.GetTVShowSeason(episode.SeasonId);
                        vm.Picture = season.Picture;
                    }
                    if (vm.Picture == null)
                    {
                        show = await MediaLibrary.GetTVShow((season == null) ? 0 : season.ShowId);
                        vm.Picture = show.Picture;
                    }
                }
                else if (item.TypedItem is Movie)
                {
                    vm = new MovieListItemViewModel(item.TypedItem as Movie,
                                                    () => null,
                                                    StatusPublisher,
                                                    NavigationManager);
                }
                else
                    continue;
                Items.Insert(0, vm);
            }
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LoadItems();
        }

        private void LoadItems()
        {
            AddNewItems(_PlaybackHistoryManager.CurrentHistory.Items);
        }

    }
}
