using Renci.SshNet;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.ViewModels.MediaLists.Details
{
    public class TVShowDetailsViewModel: BaseDetailsViewModel
    {
        public TVShowDetailsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settings,
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager)
            : base(statusPublisher, navigationManager, settings, downloadManager, mediaLibrary)
        {
            DownloadSeason = new Command(() => ExecuteDownloadSeason(), () => CanDownloadSeason());
            ToggleSetup = new Command(() =>ExecuteToggleSetup());
        }


        private void ExecuteToggleSetup()
        {
            IsSetupVisible = !IsSetupVisible;
        }

        public void SetParent(TVShow show, TVShowSeason season, TVShowEpisode episode)
        {
            Episode = episode;
            Season = season;
            Show = show;            
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
                base.Collection = show as BaseModel ?? Season as BaseModel ?? Episode as BaseModel;
                Title = show?.Name ?? Episode?.Name ?? Season?.Name;
                Banner = show?.Banner ?? Season?.Banner;
            }
        }

        private TVShowSeason season;
        private TVShowSeason Season { get { return season; } 
            set 
            {
                season = value;
                base.Collection = show as BaseModel ?? Season as BaseModel ?? Episode as BaseModel;
            } 
        }

        private TVShowSeason _SelectedSeason = null;

        private TVShowEpisode episode;
        private TVShowEpisode Episode
        {
            get { return episode; }
            set
            {
                episode = value;
                base.Collection = show as BaseModel ?? Season as BaseModel ?? Episode as BaseModel;
            }
        }

        public Command DownloadSeason { get; }
        public Command ToggleSetup { get; }
        public Command DeleteShow { get; }

        private bool CanDownloadSeason()
        {
            return SelectedSeason != null && !_DownloadStarted;
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

        public ObservableCollection<TVShowSeason> Seasons { get; } = new ObservableCollection<TVShowSeason>();

        public ObservableCollection<TVShowEpisodeListItemViewModel> Episodes { get; } = new ObservableCollection<TVShowEpisodeListItemViewModel>();

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

        public bool IsSetupVisible {
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
