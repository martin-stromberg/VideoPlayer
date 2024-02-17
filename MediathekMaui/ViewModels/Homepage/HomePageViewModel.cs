using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.MediaLibrary.PlaybackHistory;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.Homepage
{
    public class HomePageViewModel: BaseViewModel
    {

        public HomePageViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            IPlaybackHistoryManager playbackHistoryManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager)
            : base(statusPublisher, navigationManager, settingsService)
        {
            LatestViews = new LatestViewsViewModel(StatusPublisher,
                                                   navigationManager,
                                                   mediaLibrary,
                                                   playlistManager,
                                                   playbackHistoryManager,
                                                   settingsService,
                                                   downloadManager);
            OpenCategory = new Command((sender) => DoOpenCategory(sender));
            ToggleLatestViewEditMode = new Command(() => ExecuteToggleLatestViewEditMode());
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LatestViews.OnAppeared();
        }

        public override void OnDisappeared(bool closing)
        {
            base.OnDisappeared(closing);
            LatestViews.OnDisappeared(closing);
        }

        public Command OpenCategory { get; }

        public LatestViewsViewModel LatestViews { get; }

        public Command ToggleLatestViewEditMode { get; }

        private void ExecuteToggleLatestViewEditMode()
        {
            LatestViews.ToggleEditMode();
        }

        private void DoOpenCategory(object cmd)
        {
            switch ((string)cmd)
            {
                case "movies":
                    NavigationManager.OpenMovies();
                    break;
                case "tvshows":
                    NavigationManager.OpenTVShows();
                    break;
            }
        }

    }
}
