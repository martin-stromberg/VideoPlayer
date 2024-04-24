using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Classification;
using Mediathek.Services.MediaLibrary.Demo;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.MediaLibrary.OverviewPreparation;
using Mediathek.Services.MediaLibrary.PlaybackHistory;
using Mediathek.Services.MediaLibrary.Scanner;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Mediathek.ViewModels.Homepage;

namespace Mediathek.ViewModels.Global
{
    public class ApplicationViewModel: BaseViewModel
    {

        public static ApplicationViewModel Empty()
        {
            return new ApplicationViewModel() { IsDummy = true };
        }

        public void Fill(ApplicationViewModel viewModel)
        {
            IsDummy = false;
            StatusPublisher = viewModel.StatusPublisher;
            NavigationManager = viewModel.NavigationManager;
            Settings = viewModel.Settings;
            _DownloadManager = viewModel._DownloadManager;
            _PlaybackHistoryManager = viewModel._PlaybackHistoryManager;
            _PlaylistManager = viewModel._PlaylistManager;
            _UserSecrets = viewModel._UserSecrets;
            _MediaItemClassifier = viewModel._MediaItemClassifier;
            _LibraryScanner = viewModel._LibraryScanner;
            _DemoLibrary = viewModel._DemoLibrary;
            _MediaLibrary = viewModel._MediaLibrary;
            _OverviewManager = viewModel._OverviewManager;
            Title = viewModel.Title;
            StatusViewModel = viewModel.StatusViewModel;
            ContentViewModel = viewModel.ContentViewModel;
        }

        private IMediaLibrary _MediaLibrary;
        private DemoLibrary _DemoLibrary;
        private ILibraryScanner _LibraryScanner;
        private IMediaItemClassifier _MediaItemClassifier;
        private IUserSecrets _UserSecrets;
        private IPlaylistManager _PlaylistManager;
        private IPlaybackHistoryManager _PlaybackHistoryManager;
        private IDownloadManager _DownloadManager;
        private IOverviewManager _OverviewManager;

        private ApplicationViewModel()
            : this(null, null, null, null, null, null, null, null, null, null, null, null, null, null) { }

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
            IPlaybackHistoryManager playbackHistoryManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager,
            IOverviewManager overviewManager)
            : base(statusPublisher, navigationManager, settingsService)
        {
            _OverviewManager = overviewManager;
            IsDummy = false;
            _DownloadManager = downloadManager;
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
            ListMediaItems = new Command((arg) => DoListMediaItems(arg));
            IsInitializing = true;
        }

        public bool IsDummy { get; private set; }

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
                if (IsDummy)
                    return;
                AddStatusMessage("Initialisiere...");
                await InitializeSettings();
                await InitializeSecrets();
                await CheckAddDemoLibraryAsync();
                await CheckDataUpgrade();
                await InitGeneralPlaylistAsync();
                await InitPlaybackHistory();
                StartDownloadsAsync();
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
                if (!IsDummy)
                    ContentViewModel?.OnAppeared();
            }
            IsInitialized = true;
        }

        private async Task CheckDataUpgrade()
        {
            AddStatusMessage("Führe Datenupgrade aus...");
            var version = Settings.Current.DataVersion;
            switch (version)
            {
                case 0:
                    await ExecuteDataUpgrade1();
                    break;
                case 1:
                    await ExecuteDataUpgrade2();
                    break;
                case 2:
                    await ExecuteDataUpgrade3();
                    break;
            }
        }

        private async Task ExecuteDataUpgrade2()
        {
            await _OverviewManager.RecreateData();
            Settings.Current.DataVersion = 2;
        }

        private async Task ExecuteDataUpgrade3()
        {
            await _OverviewManager.RecreateData();
            Settings.Current.DataVersion = 3;
        }

        private async Task ExecuteDataUpgrade1()
        {
            await _MediaLibrary.ResetMediaItemDates();
            Settings.Current.DataVersion = 1;
        }

        private async Task InitializeSettings()
        {
            AddStatusMessage("Lade Programmeinstellungen...");
            await Settings.InitializeAsync();
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

        private void StartDownloadsAsync()
        {
            AddStatusMessage("Starte Downloads...");
            _DownloadManager.ContinueDownloads();
        }

        private void StartLibraryScans()
        {
            AddStatusMessage("Starte Quellscanner...");
            if (Settings.Current.LibraryScan_AutomaticScan)
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

        public Command ListMediaItems { get; set; }

        private void DoListMediaItems(object arg)
        {
            switch (arg)
            {
                case "uncategorized":
                    NavigationManager.OpenUncategrized();
                    break;
                case "downloads":
                    NavigationManager.OpenDownloads();
                    break;
                default:
                    throw new NotImplementedException($"{arg}");
            }
        }

    }
}
