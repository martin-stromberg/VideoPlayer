using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.MediaLibrary.PlaybackHistory;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Mediathek.ViewModels.MediaLists;
using Mediathek.ViewModels.MediaLists.MediaListItem;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;

namespace Mediathek.ViewModels.Homepage
{
    public class LatestViewsViewModel: BaseMediaListViewModel
    {

        private readonly IPlaybackHistoryManager _PlaybackHistoryManager;

        public LatestViewsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            IPlaybackHistoryManager playbackHistoryManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager)
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager, settingsService, downloadManager)
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
                BaseMediaListItemViewModel vm = Items.FirstOrDefault(i => i.Item.Id == item.TypedItem.Id);
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
                BaseMediaListItemViewModel vm;
                if (item.TypedItem is TVShowEpisode)
                {
                    var episode = item.TypedItem as TVShowEpisode;
                    TVShowSeason season = null;
                    TVShow show;
                    vm = new TVShowEpisodeListItemViewModel(episode,
                                                            () => null,
                                                            StatusPublisher,
                                                            NavigationManager,
                                                            Settings,
                                                            DownloadManager,
                                                            MediaLibrary)
                    {
                        Playlist = item.Playlist
                    };
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
                                                    NavigationManager,
                                                    Settings,
                                                    DownloadManager,
                                                    MediaLibrary)
                    {
                        Playlist = item.Playlist
                    };
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
