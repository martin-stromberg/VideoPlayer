using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.MediaLibrary.Scanner;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Mediathek.ViewModels.MediaLists.MediaListItem;
using Mediathek.ViewModels.VideoPlayer;
using System.ComponentModel;

namespace Mediathek.ViewModels.MediaLists.Details
{
    public class MovieDetailsViewModel: BaseDetailsViewModel
    {

        private readonly IPlaylistManager playlistManager;

        public MovieDetailsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settings,
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            IPlaylistManager playlistManager,
            ILibraryScanner libraryScanner,
            VideoPlayerViewModel videoPlayerViewModel)
            : base(statusPublisher, navigationManager, settings, downloadManager, mediaLibrary, libraryScanner)
        {
            CollectionViewModel = new MovieCollectionViewModel(StatusPublisher,
                                                               navigationManager,
                                                               mediaLibrary,
                                                               playlistManager,
                                                               settings,
                                                               downloadManager);
            this.playlistManager = playlistManager;
            PlayerViewModel = videoPlayerViewModel;
            PlayerViewModel.PropertyChanged += PlayerViewModel_PropertyChanged;
            Play = new Command(() => ExecutePlay(), () => CanExecutePlay());
            ToggleSetup = new Command(() => ExecuteToggleSetup());
            DownloadCollection = new Command(() => ExecuteDownloadCollection());
            IsVideoStopped = true;
            IsVideoInWindowMode = true;
        }

        private async void ExecuteDownloadCollection()
        {
            var sessions = (await DownloadManager.StartDownloadAsync(CurrentMediaCollection, MediaItemCopyType.Download)).ToList();
            sessions.AddRange(await DownloadManager.StartDownloadAsync(Movie, MediaItemCopyType.Download));
            foreach (var movie in CollectionViewModel.Items.Select(vm => vm.Item))
                sessions.AddRange(await DownloadManager.StartDownloadAsync(movie, MediaItemCopyType.Download));
        }

        private void ExecuteToggleSetup()
        {
            IsSetupVisible = !IsSetupVisible;
        }

        public bool IsSetupVisible
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

        private void PlayerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(VideoPlayerViewModel.ErrorMessage):
                    PlaybackErrorMessage = PlayerViewModel.ErrorMessage;
                    IsVideoStopped = true;
                    break;
                case nameof(VideoPlayerViewModel.IsFullScreen):
                    IsVideoInFullscreen = PlayerViewModel.IsFullScreen;
                    break;
            }
        }

        private bool CanExecutePlay()
        {
            return SelectedMovie != null;
        }

        public Movie SelectedMovie
        {
            get
            {
                return GetProperty<Movie>();
            }
            set
            {
                SetProperty<Movie>(value);
                Play.ChangeCanExecute();
            }
        }

        private async void ExecutePlay()
        {
            await playlistManager.StartMoviePlaylistAsync(SelectedMovie, () =>
                                                                         CollectionViewModel.Items
                                                                                            .Where(m =>
                                                                                                   m.Item.Id != SelectedMovie.Id)
                                                                                            .Cast<BaseModel>());
            PlayerViewModel.VideoSource = null;
            PlayerViewModel.OnAppeared();
            IsVideoPlaying = true;
        }

        public bool IsVideoStopped
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsVideoPlaying = false;
            }
        }

        public bool IsVideoPlaying
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsVideoStopped = false;
            }
        }

        public string PlaybackErrorMessage
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
                IsPlaybackErrorMessageVisible = !string.IsNullOrWhiteSpace(value);
            }
        }

        public bool IsPlaybackErrorMessageVisible
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

        public int VideoPlayerRowSpan
        {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(value);
            }
        }

        public bool IsVideoInFullscreen
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsVideoInWindowMode = false;
                else if (!IsVideoInWindowMode)
                    IsVideoInWindowMode = true;
                VideoPlayerRowSpan = value ? 2 : 1;
            }
        }

        public bool IsVideoInWindowMode
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsVideoInFullscreen = false;
            }
        }

        public void SetParent(MovieCollection movieCollection, Movie movie)
        {
            MovieCollection = movieCollection;
            Movie = movie;
        }

        public MovieCollection MovieCollection
        {
            get
            {
                return GetProperty<MovieCollection>();
            }
            set
            {
                SetProperty<MovieCollection>(value);
                ProcessParentChanged();
            }
        }

        public Movie Movie
        {
            get
            {
                return GetProperty<Movie>();
            }
            set
            {
                SetProperty<Movie>(value);
                ProcessParentChanged();
            }
        }

        public VideoPlayerViewModel PlayerViewModel { get; }

        public Command Play { get; }

        public Command ToggleSetup { get; }

        public Command DownloadCollection { get; }

        private void ProcessParentChanged()
        {
            CurrentMediaCollection = MovieCollection as BaseModel ?? Movie as BaseModel;
            Title = Movie?.Name ?? MovieCollection?.Name;
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork_LoadContent;
            ;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted_LoadContent;
            ;
            worker.RunWorkerAsync();
        }

        private void Worker_RunWorkerCompleted_LoadContent(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!e.Cancelled)
                (sender as BackgroundWorker).RunWorkerAsync();
        }

        private void Worker_DoWork_LoadContent(object sender, DoWorkEventArgs e)
        {
            Thread.Sleep(100);
            e.Cancel = true;
            LoadMovies();
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                SelectedMovie = Movie;
                if (Movie == null)
                    SelectedMovie = CollectionViewModel.Items.FirstOrDefault()?.Item as Movie;
            });
        }

        public MovieCollectionViewModel CollectionViewModel { get; }

        private async void LoadMovies()
        {
            if ((Movie != null) && (MovieCollection == null) && (Movie.CollectionId != 0))
                MovieCollection = await MediaLibrary.GetMovieCollection(Movie.CollectionId);
            if (MovieCollection != null)
            {
                var movies = await MediaLibrary.GetMovies(MovieCollection.Id);
                foreach (var movie in movies
                    .OrderBy(m => m.Date)
                    .ThenBy(m => m.Name))
                {
                    var vm = new MovieListItemViewModel(movie,
                                                        () => null,
                                                        StatusPublisher,
                                                        NavigationManager,
                                                        Settings,
                                                        DownloadManager,
                                                        MediaLibrary)
                    {
                        Mode = ItemViewModel.Box
                    };
                    Add(vm);
                }
            }
            if (Movie != null)
            {
                var vm = new MovieListItemViewModel(Movie,
                                                    () => null,
                                                    StatusPublisher,
                                                    NavigationManager,
                                                    Settings,
                                                    DownloadManager,
                                                    MediaLibrary)
                {
                    Mode = ItemViewModel.Box
                };
                Add(vm);
            }
        }

        private void Add(MovieListItemViewModel vm)
        {
            vm.BeforeOpenDetails += Vm_BeforeOpenDetails;
            if (!CollectionViewModel.Items.Any(existing => existing.Item.Id == vm.Item.Id))
                CollectionViewModel.Items.Add(vm);
        }

        private void Vm_BeforeOpenDetails(object sender, BaseModelProcessEventArgs e)
        {
            e.Continue = false;
            SelectedMovie = e.Element as Movie;
            if (IsVideoPlaying)
                ExecutePlay();
        }

    }
}
