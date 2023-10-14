using CommunityToolkit.Maui.Views;
using MyVideoPlayer.Helper;
using MyVideoPlayer.Helper.Download;
using MyVideoPlayer.Helper.LibraryScan;
using MyVideoPlayer.Helper.Navigation;
using MyVideoPlayer.ViewModels.Logs;
using MyVideoPlayer.ViewModels.Menu;
using MyVideoPlayer.ViewModels.Navigation;
using MyVideoPlayer.ViewModels.Navigation.Sources;
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
            NavigateToControlPanel = new Command(() => DoNavigateToControlPanel());
            MediaElement = new MediaElementViewModel();
            MediaElement.PropertyChanged += MediaElement_PropertyChanged;
            MediaElement.OnMediaEnded += MediaElement_OnMediaEnded;

            this.mediaLibrary = mediaLibrary;
            this.libraryDownloader = libraryDownloader;
            this.navigationManager = navigationManager;
            this.libraryScanner = libraryScanner;
            this.libraryScanner.StatusChanged += (sender, e) => { StatusMessage = e.Message; };
            this.navigationManager.NavigationCompleted += (sender, e) => { NavigationContent = e.ContentViewModel; };
            this.navigationManager.MenuChanged += (sender, e) => { MenuContent = e.ViewModel; };
            this.navigationManager.MediaSourceToPlay += (sender, e) => { MediaElement.Play(e.MediaSource); };
            Title = "Medienbibliothek";
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

        public Command NavigateBack { get; }

        private void DoNavigateBack()
        {
            if (IsControlPanelVisible)
                IsControlPanelVisible = false;
            else if (MediaElement.IsPlaying())
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

        #region Menu
        public bool MenuVisible
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

        public MenuViewModel MenuContent
        {
            get
            {
                return GetProperty<MenuViewModel>();
            }
            set
            {
                SetProperty<MenuViewModel>(value);
                MenuVisible = value != null;
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
                LogVisible = value is LogListViewModel;
                SourceConfigurationVisible = value is SourceConfigurationViewModel;
                NavigationVisible = !LogVisible && !SourceConfigurationVisible && (value != null);
                if (value != null)
                {
                    Title = value.Title;
                    StartLoadNavigationContent();
                }
                else
                    Title = "Medienbibliothek";
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

        #region ControlPanel 
        public Command NavigateToControlPanel { get; }

        private void DoNavigateToControlPanel()
        {
            IsControlPanelVisible = true;
        }

        public bool IsControlPanelVisible
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

        private ControlPanelViewModel controlPanelViewModel;

        public ControlPanelViewModel ControlPanelViewModel
        {
            get
            {
                if (controlPanelViewModel == null)
                {
                    controlPanelViewModel = _serviceProvider.GetService<ControlPanelViewModel>();
                    controlPanelViewModel.CloseRequested += (sender, e) =>
                    {
                        if (IsControlPanelVisible)
                            NavigateBack.Execute(null);
                    };
                }
                return controlPanelViewModel;
            }
        }
        #endregion

        public bool SourceConfigurationVisible
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

    }
}
