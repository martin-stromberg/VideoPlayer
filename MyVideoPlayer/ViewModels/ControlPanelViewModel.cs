using MyVideoPlayer.Helper.LibraryScan;
using MyVideoPlayer.Helper.Navigation;
using System;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary;

namespace MyVideoPlayer.ViewModels
{
    public class ControlPanelViewModel: BaseViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly INavigationManager _NavigationManager;
        private readonly ILibraryScanner _LibraryScanner;

        public ControlPanelViewModel(
            IMediaLibrary mediaLibrary,
            INavigationManager navigationManager,
            ILibraryScanner libraryScanner)
            : base()
        {
            _LibraryScanner = libraryScanner;
            _NavigationManager = navigationManager;
            _MediaLibrary = mediaLibrary;
            CleanScan = new Command(async () => await DoCleanScanAsync());
            ShowLog = new Command(() => DoShowLog());
            NavigateToSources = new Command(() => DoNavigateToSources());
        }

        public Command ShowLog { get; }

        public Command CleanScan { get; }

        private async Task DoCleanScanAsync()
        {
            _LibraryScanner.Stop();
            await Task.Delay(1000);
            await _MediaLibrary.ClearMedia();
            _LibraryScanner.Start();
            OnCloseRequested();
        }

        private void DoShowLog()
        {
            _NavigationManager.NavigateToLog();
            OnCloseRequested();
        }

        public event EventHandler CloseRequested;

        private void OnCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public Command NavigateToSources { get; }

        private void DoNavigateToSources()
        {
            _NavigationManager.NavigateToSourceOverview();
            OnCloseRequested();
        }

    }
}
