using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.MediaLibrary.PlaybackHistory;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Homepage
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
            IMediaDownloader mediaDownloader)
            : base(statusPublisher, navigationManager, settingsService)
        {
            LatestViews = new LatestViewsViewModel(StatusPublisher,
                                                   navigationManager,
                                                   mediaLibrary,
                                                   playlistManager,
                                                   playbackHistoryManager,
                                                   settingsService,
                                                   mediaDownloader);
            OpenCategory = new Command((sender) => DoOpenCategory(sender));
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
