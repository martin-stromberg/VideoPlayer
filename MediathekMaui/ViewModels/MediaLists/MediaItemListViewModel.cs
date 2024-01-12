using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.MediaLibrary.Scanner;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Mediathek.ViewModels.MediaLists.MediaListItem;
using System;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists
{
    public class MediaItemListViewModel: BaseMediaListViewModel
    {

        public MediaItemListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            ILibraryScanner libraryScanner,
            IDownloadManager downloadManager)
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager, settingsService, downloadManager)
        {
            _LibraryScanner = libraryScanner;
        }

        protected override void ProcessMediaItemRemoved(MediaItem mediaItem)
        {
            base.ProcessMediaItemRemoved(mediaItem);
            var listEntry = Items.FirstOrDefault(vm => vm.Item.Id == mediaItem.Id);
            if (listEntry != null)
                Items.Remove(listEntry);
        }

        public string Category
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                if (Category != value)
                    Clear();
                SetProperty<string>(value);
                if (isAppeared)
                    StartLoadItems();
            }
        }

        private bool isAppeared = false;

        public override void OnAppeared()
        {
            base.OnAppeared();
            StartLoadItems();
            isAppeared = true;
        }

        public override void OnDisappeared(bool closing)
        {
            base.OnDisappeared(closing);
            isAppeared = false;
            currentLoadingSession = 0;
        }

        private int currentLoadingOffset = 0;
        private int currentLoadingCount = 10;
        private long currentLoadingSession = 0;
        private readonly ILibraryScanner _LibraryScanner;

        private void Clear()
        {
            currentLoadingSession = 0;
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
            MediaItem[] items = null;
            switch (Category)
            {
                case "downloads":
                    items = (await MediaLibrary.GetDownloadedMediaItems(currentLoadingOffset, currentLoadingCount)).ToArray();
                    break;
                default:
                    items = (await MediaLibrary.GetUncategorizedMediaItems(currentLoadingOffset, currentLoadingCount)).ToArray();
                    break;
            }
            foreach (var item in items)
                if (currentLoadingSession == session)
                    Items.Add(new MediaListItemViewModel(item,
                                                         StatusPublisher,
                                                         NavigationManager,
                                                         Settings,
                                                         _LibraryScanner,
                                                         DownloadManager,
                                                         MediaLibrary)
                        {
                            Mode = ItemViewModel.Lane
                        });
            currentLoadingOffset += items.Length;
            if (items.Length > 0)
                await LoadNextItems(session);
        }

    }
}
