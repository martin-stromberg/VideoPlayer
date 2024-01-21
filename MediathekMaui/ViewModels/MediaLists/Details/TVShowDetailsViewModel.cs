using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Mediathek.ViewModels.MediaLists.MediaListItem;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists.Details
{
    public class TVShowDetailsViewModel: BaseDetailsViewModel
    {

        public TVShowDetailsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settings,
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            IPlaylistManager playlistManager)
            : base(statusPublisher, navigationManager, settings, downloadManager, mediaLibrary)
        {
            _PlaylistManager = playlistManager;
            DownloadSeason = new Command(() => ExecuteDownloadSeason(), () => CanDownloadSeason());
            ToggleSetup = new Command(() => ExecuteToggleSetup());
        }

        private void ExecuteToggleSetup()
        {
            IsSetupVisible = !IsSetupVisible;
        }

        public void SetParent(TVShowCollection collection, TVShow show, TVShowSeason season, TVShowEpisode episode)
        {
            Collection = collection;
            Episode = episode;
            Season = season;
            Show = show;
        }

        private TVShowCollection collection;

        public TVShowCollection Collection
        {
            get
            {
                return collection;
            }
            set
            {
                collection = value;
            }
        }

        public TVShow show;

        public TVShow Show
        {
            get
            {
                return show;
            }
            set
            {
                show = value;
                CurrentMediaCollection = collection as BaseModel ?? show as BaseModel ?? Season as BaseModel ?? Episode as BaseModel;
                Title = collection?.Name ?? show?.Name ?? Episode?.Name ?? Season?.Name;
                Banner = show?.Banner ?? Season?.Banner;
                IsShowSelected = value != null;
            }
        }

        public bool IsShowSelected
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                IsShowCollectionVisible = !value && (Collection is not null);
            }
        }

        public bool IsShowCollectionVisible
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsEpisodeLisTVisible = false;
            }
        }

        public bool IsEpisodeLisTVisible
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsShowCollectionVisible = false;
            }
        }

        private TVShowSeason season;

        private TVShowSeason Season
        {
            get
            {
                return season;
            }
            set
            {
                season = value;
                CurrentMediaCollection = collection as BaseModel ?? show as BaseModel ?? Season as BaseModel ?? Episode as BaseModel;
            }
        }

        private TVShowSeason _SelectedSeason = null;

        private TVShowEpisode episode;

        private TVShowEpisode Episode
        {
            get
            {
                return episode;
            }
            set
            {
                episode = value;
                CurrentMediaCollection = collection as BaseModel ?? show as BaseModel ?? Season as BaseModel ?? Episode as BaseModel;
            }
        }

        public Command DownloadSeason { get; }

        public Command ToggleSetup { get; }

        public Command DeleteShow { get; }

        private bool CanDownloadSeason()
        {
            return (SelectedSeason != null) && !_DownloadStarted;
        }

        private bool _DownloadStarted = false;

        private async void ExecuteDownloadSeason()
        {
            var sessions = await StartDownload(SelectedSeason);
            _DownloadStarted = true;
            DownloadSeason?.ChangeCanExecute();
        }

        public TVShowSeason SelectedSeason
        {
            get
            {
                return GetProperty<TVShowSeason>();
            }
            set
            {
                SetProperty<TVShowSeason>(value);
                _DownloadStarted = false;
                DownloadSeason?.ChangeCanExecute();
                LoadEpisodes(Episode);
            }
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            DateTime ExecutionTime = DateTime.Now;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (sender, e) =>
            {
                Thread.Sleep(100);
                if (DateTime.Now > ExecutionTime)
                {
                    e.Cancel = true;
                    if (Collection is not null)
                        LoadShows();
                    else
                        LoadSeasons(Season, Episode);
                    Season = null;
                }
            };
            worker.RunWorkerCompleted += (sender, e) =>
            {
                if (!e.Cancelled)
                    worker.RunWorkerAsync();
            };
            worker.RunWorkerAsync();
        }

        public ImageSource Banner
        {
            get
            {
                return GetProperty<ImageSource>();
            }
            set
            {
                SetProperty<ImageSource>(value);
            }
        }

        private TVShowListViewModel showsViewModel = null;

        public TVShowListViewModel ShowsViewModel
        {
            get
            {
                if (showsViewModel == null)
                    showsViewModel = new TVShowListViewModel(StatusPublisher,
                                                             NavigationManager,
                                                             MediaLibrary,
                                                             _PlaylistManager,
                                                             Settings,
                                                             DownloadManager);
                return showsViewModel;
            }
        }

        public ObservableCollection<TVShowListItemViewModel> Shows { get; } = new ObservableCollection<TVShowListItemViewModel>();

        public ObservableCollection<TVShowSeason> Seasons { get; } = new ObservableCollection<TVShowSeason>();

        public ObservableCollection<TVShowEpisodeListItemViewModel> Episodes { get; } = new ObservableCollection<TVShowEpisodeListItemViewModel>();

        private void LoadShows()
        {
            ShowsViewModel.SetParent(Collection, Show, Season);
            ShowsViewModel.OnAppeared();

            // var shows = await MediaLibrary.GetTVShows(Collection.Id);
            // await MainThread.InvokeOnMainThreadAsync(() => { Shows.Clear(); });
            // foreach (var show in shows
            // .OrderBy(show => show.PremieredAt)
            // .ThenBy(show => show.Name))
            // {
            // IsShowCollectionVisible = true;
            // await AddShowAsync(show);
            // }
        }

        private async Task AddShowAsync(TVShow show)
        {
            var vm = new TVShowListItemViewModel(show,
                                                 StatusPublisher,
                                                 NavigationManager,
                                                 _PlaylistManager,
                                                 Settings,
                                                 DownloadManager,
                                                 MediaLibrary)
            {
                Mode = ItemViewModel.Box
            };
            await MainThread.InvokeOnMainThreadAsync(() => { Shows.Add(vm); });
        }

        private async void LoadSeasons(TVShowSeason initSeason, TVShowEpisode initEpisode)
        {
            if (initSeason == null & initEpisode != null)
                initSeason = await MediaLibrary.GetTVShowSeason(initEpisode.SeasonId);
            if ((Show == null) && (initSeason != null))
                Show = await MediaLibrary.GetTVShow(initSeason.ShowId);

            var currentSeason = SelectedSeason;
            var seasons = await MediaLibrary.GetTVShowSeasons(Show.Id);
            TVShowSeason seasonToSelect = null;
            await MainThread.InvokeOnMainThreadAsync(() => { Seasons.Clear(); });
            foreach (var season in seasons.OrderBy(season => season.Name))
            {
                await MainThread.InvokeOnMainThreadAsync(() => { Seasons.Add(season); });
                if ((currentSeason != null) && (season.Id == currentSeason.Id))
                    seasonToSelect = season;
                else if ((initSeason != null) && (season.Id == initSeason.Id))
                    seasonToSelect = season;
            }
            if (seasonToSelect == null)
                seasonToSelect = Seasons.FirstOrDefault();
            IsEpisodeLisTVisible = true;
            await MainThread.InvokeOnMainThreadAsync(() => { SelectedSeason = seasonToSelect; });
        }

        public bool Loading
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

        private long loadSessionId = 0;
        private readonly IPlaylistManager _PlaylistManager;

        private async void LoadEpisodes(TVShowEpisode initEpisode)
        {
            loadSessionId = DateTime.Now.Ticks;
            await LoadEpisodes(loadSessionId, initEpisode);
        }

        private async Task LoadEpisodes(long sessionId, TVShowEpisode initEpisode)
        {
            Loading = true;
            try
            {
                Episodes.Clear();
                if (SelectedSeason == null)
                    return;
                if (loadSessionId != sessionId)
                    return;
                Banner = SelectedSeason.Banner ?? Show.Banner;
                var episodes = await MediaLibrary.GetTVShowEpisodes(SelectedSeason.Id);
                TVShowEpisode selectedEpisode = null;
                foreach (var episode in episodes
                    .OrderBy(episode => episode.EpisodeNo)
                    .ThenBy(episode => episode.Part))
                {
                    var vm = new TVShowEpisodeListItemViewModel(episode,
                                                                () => null,
                                                                StatusPublisher,
                                                                NavigationManager,
                                                                Settings,
                                                                DownloadManager,
                                                                MediaLibrary)
                    {
                        Mode = ItemViewModel.Lane
                    };
                    if (loadSessionId != sessionId)
                        break;
                    Episodes.Add(vm);
                    if ((initEpisode != null) && (episode.Id == initEpisode.Id))
                        selectedEpisode = episode;
                }
                if (selectedEpisode != null)
                    SelectedEpisode = selectedEpisode;
            }
            finally
            {
                Loading = false;
            }
        }

        public TVShowEpisode SelectedEpisode
        {
            get
            {
                return GetProperty<TVShowEpisode>();
            }
            set
            {
                SetProperty<TVShowEpisode>(value);
            }
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

        #region Serienzuordnung zur Sammlung
        public ObservableCollection<TVShowName> UnassignedShows { get; } = new ObservableCollection<TVShowName>();

        public bool LoadingUnassignedShows
        {
            get
            {
                return GetProperty<bool>();
            }
            private set
            {
                SetProperty<bool>(value);
                LoadedUnassinedShows = !value;
            }
        }

        public bool LoadedUnassinedShows
        {
            get
            {
                return GetProperty<bool>();
            }
            private set
            {
                SetProperty<bool>(value);
            }
        }

        public async void LoadUnassignedShows()
        {
            LoadingUnassignedShows = true;
            try
            {
                var shows = await MediaLibrary.GetTVShowNames();
                await MainThread.InvokeOnMainThreadAsync(() => { UnassignedShows.Clear(); });
                foreach (var show in shows
                    .Where(s => s.CollectionId == 0)
                    .OrderBy(s => s.Name))
                {
                    await MainThread.InvokeOnMainThreadAsync(() => { UnassignedShows.Add(show); });
                }
            }
            finally
            {
                LoadingUnassignedShows = false;
            }
        }

        public void ClearUnassignedShows()
        {
            UnassignedShows.Clear();
            LoadedUnassinedShows = false;
        }

        public async void AssignShowToCollection(TVShowName showName)
        {
            var show = await MediaLibrary.GetTVShow(showName.Id);
            show.CollectionId = Collection.Id;
            await MediaLibrary.AddTVShowAsync(show);
            UnassignedShows.Remove(showName);
            await AddShowAsync(show);
        }
        #endregion

    }
}
