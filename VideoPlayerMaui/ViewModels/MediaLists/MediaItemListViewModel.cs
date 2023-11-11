using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Scanner;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class MediaItemListViewModel: BaseMediaListViewModel
    {

        public MediaItemListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            ILibraryScanner libraryScanner)
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager, settingsService)
        {
            _LibraryScanner = libraryScanner;
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            StartLoadItems();
        }

        public override void OnDisappeared(bool closing)
        {
            base.OnDisappeared(closing);
            currentLoadingSession = 0;
        }

        private int currentLoadingOffset = 0;
        private int currentLoadingCount = 10;
        private long currentLoadingSession = 0;
        private readonly ILibraryScanner _LibraryScanner;

        private void Clear()
        {
            Items.Clear();
            currentLoadingOffset = 0;
        }

        private async void StartLoadItems()
        {
            currentLoadingSession = DateTime.Now.Ticks;
            await Task.Delay(500);
            Clear();
            await LoadNextItems(currentLoadingSession);
        }

        private async Task LoadNextItems(long session)
        {
            if (currentLoadingSession != session)
                return;
            var items = (await MediaLibrary.GetUncategorizedMediaItems(currentLoadingOffset, currentLoadingCount)).ToArray();
            foreach (var item in items)
                if (currentLoadingSession == session)
                    Items.Add(new MediaListItemViewModel(item,
                                                         StatusPublisher,
                                                         NavigationManager,
                                                         Settings,
                                                         _LibraryScanner)
                        {
                            Mode = ItemViewModel.Lane
                        });
            currentLoadingOffset += items.Length;
            if (items.Length > 0)
                await LoadNextItems(session);
        }

    }
}
