using CommunityToolkit.Maui.Views;
using MyVideoPlayer.Helper;
using MyVideoPlayer.Helper.Download;
using MyVideoPlayer.Helper.LibraryScan;
using MyVideoPlayer.Helper.Navigation;
using MyVideoPlayer.ViewModels.Logs;
using MyVideoPlayer.ViewModels.Navigation;
using System;
using System.ComponentModel;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary;

namespace MyVideoPlayer.ViewModels
{
    public class HomePageViewModel: BaseViewModel
    {

        private readonly IMediaLibrary mediaLibrary;
        private readonly ILibraryDownloader libraryDownloader;
        private readonly INavigationManager navigationManager;
        private readonly ILibraryScanner libraryScanner;
        private readonly IServiceProvider _serviceProvider;

        public HomePageViewModel(
            IMediaLibrary mediaLibrary,
            ILibraryDownloader libraryDownloader,
            INavigationManager navigationManager,
            ILibraryScanner libraryScanner,
            IServiceProvider serviceProvider)
            : base()
        {
            _serviceProvider = serviceProvider;
            NavigateBack = new Command(() => DoNavigateBack());
            NavigateToSources = new Command(() => DoNavigateToSources());
            CleanScan = new Command(async () => await DoCleanScanAsync());
            ShowLog = new Command(() => DoShowLog());
            MediaElement = new MediaElementViewModel();
            MediaElement.PropertyChanged += MediaElement_PropertyChanged;
            MediaElement.OnMediaEnded += MediaElement_OnMediaEnded;

            this.mediaLibrary = mediaLibrary;
            this.libraryDownloader = libraryDownloader;
            this.navigationManager = navigationManager;
            this.libraryScanner = libraryScanner;
            this.libraryScanner.StatusChanged += (sender, e) => { StatusMessage = e.Message; };
            this.navigationManager.NavigationCompleted += (sender, e) => { NavigationContent = e.ContentViewModel; };
            this.navigationManager.MediaSourceToPlay += (sender, e) => { MediaElement.Play(e.MediaSource); };
            Title = "Medienbibliothek";
        }

        private void DoShowLog()
        {
            navigationManager.NavigateToLog();
        }

        private void MediaElement_OnMediaEnded(object sender, MediaSource e)
        {
            navigationManager.VideoClosed(e);
        }

        private void MediaElement_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MediaElement.VideoVisible):
                    NavigationVisible = !MediaElement.VideoVisible && (NavigationContent != null);
                    IsVideoVisible = MediaElement.VideoVisible;
                    break;
            }
        }

        private void DoNavigateToSources()
        {
            navigationManager.NavigateToSourceOverview();
        }

        public Command CleanScan { get; }

        public Command ShowLog { get; }

        private async Task DoCleanScanAsync()
        {
            await mediaLibrary.ClearMedia();
        }

        public Command NavigateBack { get; }

        public Command NavigateToSources { get; }

        private void DoNavigateBack()
        {
            if (MediaElement.IsPlaying())
                MediaElement.StopPlaying();
            else
                navigationManager.NavigateBack();
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

        public async void StartInitializationAsync()
        {
            if (IsInitialized)
                return;
            IsInitializing = true;
            try
            {
                if (await mediaLibrary.IsEmptyAsync())
                    await mediaLibrary.ImportAsync(new DemoLibrary(_serviceProvider.GetService<UserSecrets>()));
                navigationManager.NavigateToOverview();
                libraryScanner.Start();
            }
            finally
            {
                IsInitializing = false;
            }
            IsInitialized = true;
        }
        #endregion

        #region Log
        public bool LogVisible
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
        #endregion

        #region Navigation
        public bool NavigationVisible
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

        public NavigationContentViewModel NavigationContent
        {
            get
            {
                return GetProperty<NavigationContentViewModel>();
            }
            set
            {
                SetProperty<NavigationContentViewModel>(value);
                if (value is LogListViewModel)
                {
                    NavigationVisible = false;
                    LogVisible = true;
                    StartLoadNavigationContent();
                }
                else
                {
                    NavigationVisible = value != null;
                    if (NavigationVisible)
                    {
                        LogVisible = false;
                        StartLoadNavigationContent();
                        Title = value.Title;
                    }
                    else
                        Title = "Medienbibliothek";
                }
            }
        }

        private void StartLoadNavigationContent()
        {
            NavigationContent.OnAppeared();
        }
        #endregion

        #region MediaElement
        public bool IsVideoVisible
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

        public MediaElementViewModel MediaElement
        {
            get
            {
                return GetProperty<MediaElementViewModel>();
            }
            set
            {
                SetProperty<MediaElementViewModel>(value);
            }
        }
        #endregion

        #region Status
        public string StatusMessage
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }
        #endregion

    }
}
