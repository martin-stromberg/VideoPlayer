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

        public ControlPanelViewModel(IMediaLibrary mediaLibrary, INavigationManager navigationManager)
            : base()
        {
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
            await _MediaLibrary.ClearMedia();
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
