using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Classification;
using VideoPlayer.Services.MediaLibrary.Demo;
using VideoPlayer.Services.MediaLibrary.PlaybackHistory;
using VideoPlayer.Services.MediaLibrary.Scanner;
using VideoPlayer.Services.Playlists;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.Homepage;

namespace VideoPlayer.ViewModels.Global
{
    public class ApplicationViewModel: BaseViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly DemoLibrary _DemoLibrary;
        private readonly ILibraryScanner _LibraryScanner;
        private readonly IMediaItemClassifier _MediaItemClassifier;
        private readonly IUserSecrets _UserSecrets;
        private readonly IPlaylistManager _PlaylistManager;
        private readonly IPlaybackHistoryManager _PlaybackHistoryManager;

        public ApplicationViewModel(
            GlobalStatusViewModel statusViewModel,
            HomePageViewModel contentViewModel,
            IMediaLibrary mediaLibrary,
            DemoLibrary demoLibrary,
            IStatusPublisher statusPublisher,
            ILibraryScanner libraryScanner,
            IMediaItemClassifier mediaItemClassifier,
            IUserSecrets userSecrets,
            IPlaylistManager playlistManager,
            INavigationManager navigationManager,
            IPlaybackHistoryManager playbackHistoryManager)
            : base(statusPublisher, navigationManager)
        {
            _PlaybackHistoryManager = playbackHistoryManager;
            _PlaylistManager = playlistManager;
            _UserSecrets = userSecrets;
            _MediaItemClassifier = mediaItemClassifier;
            _LibraryScanner = libraryScanner;
            _DemoLibrary = demoLibrary;
            _MediaLibrary = mediaLibrary;
            Title = "Medienbibliothek";
            StatusViewModel = statusViewModel;
            ContentViewModel = contentViewModel;
            StartPlayback = new Command((arg) => DoStartPlayback(arg));
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LoadContent();
        }

        #region Startup initialization
        public bool IsInitialized
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public bool IsInitializing
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        private async void LoadContent()
        {
            if (IsInitialized)
                return;
            IsInitializing = true;
            try
            {
                AddStatusMessage("Initialisiere...");
                await InitializeSecrets();
                await CheckAddDemoLibraryAsync();
                await InitGeneralPlaylistAsync();
                await InitPlaybackHistory();
                StartLibraryScans();
                AddStatusMessage(string.Empty);
            }
            catch (Exception ex)
            {
                AddStatusMessage(ex.Message);
            }
            finally
            {
                IsInitializing = false;
                ContentViewModel?.OnAppeared();
            }
            IsInitialized = true;
        }

        private async Task InitPlaybackHistory()
        {
            AddStatusMessage("Initialisiere Abspielhistorie...");
            await _PlaybackHistoryManager.InitializeAsync();
        }

        private async Task InitGeneralPlaylistAsync()
        {
            AddStatusMessage("Initialisiere Playlists...");
            await _PlaylistManager.InitializeAsync();
        }

        private async Task InitializeSecrets()
        {
            await _UserSecrets.Initialize();
        }

        private void StartLibraryScans()
        {
            AddStatusMessage("Starte Quellscanner...");
            _LibraryScanner.Start();
        }

        private async Task CheckAddDemoLibraryAsync()
        {
            if (!(await _MediaLibrary.IsEmptyAsync()))
                return;
            AddStatusMessage("Lade Demodaten...");
            _DemoLibrary.Fill();
            await _MediaLibrary.ImportAsync(_DemoLibrary);
        }
        #endregion

        public HomePageViewModel ContentViewModel
        {
            get
            {
                return GetProperty<HomePageViewModel>();
            }
            set
            {
                SetProperty<HomePageViewModel>(value);
            }
        }

        public GlobalStatusViewModel StatusViewModel
        {
            get
            {
                return GetProperty<GlobalStatusViewModel>();
            }
            set
            {
                SetProperty<GlobalStatusViewModel>(value);
            }
        }

        public Command StartPlayback { get; set; }

        private async void DoStartPlayback(object arg)
        {
            await NavigationManager.OpenPlaylistPlaybackAsync();
        }

    }
}
