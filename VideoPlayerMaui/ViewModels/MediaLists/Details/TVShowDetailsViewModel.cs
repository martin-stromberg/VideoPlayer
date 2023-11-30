using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.ViewModels.MediaLists.Details
{
    public class TVShowDetailsViewModel: BaseViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly IMediaDownloader _MediaDownloader;

        public TVShowDetailsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settings,
            IMediaLibrary mediaLibrary,
            IMediaDownloader mediaDownloader)
            : base(statusPublisher, navigationManager, settings)
        {
            _MediaDownloader = mediaDownloader;
            _MediaLibrary = mediaLibrary;
            DownloadSeason = new Command(() => ExecuteDownloadSeason(), () => CanDownloadSeason());

            // for (int idx = 0; idx < 100; idx++)
            // Episodes.Add(new TVShowEpisodeListItemViewModel(new TVShowEpisode()
            // {
            // EpisodeNo = "{idx}",
            // Id = 0,
            // MediaItems = new long[] { },
            // SeasonId = 0,
            // SeasonName = ".",
            // Name = ".",
            // ShowName = string.Empty
            // },
            // () => null,
            // StatusPublisher,
            // navigationManager,
            // settings,
            // mediaDownloader,
            // mediaLibrary)
            // {
            // Mode = ItemViewModel.Dummy
            // });
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
                Title = show?.Name ?? Episode?.Name ?? Season?.Name;
                Banner = show?.Banner ?? Season?.Banner;
            }
        }

        private TVShowSeason Season { get; set; }

        private TVShowSeason _SelectedSeason = null;

        private TVShowEpisode Episode { get; set; }

        public Command DownloadSeason { get; }

        private bool CanDownloadSeason()
        {
            return SelectedSeason != null;
        }

        private void ExecuteDownloadSeason()
        {
            _MediaDownloader.StartDownload(SelectedSeason);
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
                initSeason = await _MediaLibrary.GetTVShowSeason(initEpisode.SeasonId);
            if ((Show == null) && (initSeason != null))
                Show = await _MediaLibrary.GetTVShow(initSeason.ShowId);

            var currentSeason = SelectedSeason;
            var seasons = await _MediaLibrary.GetTVShowSeasons(Show.Id);
            TVShowSeason seasonToSelect = null;
            await MainThread.InvokeOnMainThreadAsync(() => { Seasons.Clear(); });
            foreach (var season in seasons)
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
                var episodes = await _MediaLibrary.GetTVShowEpisodes(SelectedSeason.Id);
                TVShowEpisode selectedEpisode = null;
                foreach (var episode in episodes)
                {
                    var vm = new TVShowEpisodeListItemViewModel(episode,
                                                                () => null,
                                                                StatusPublisher,
                                                                NavigationManager,
                                                                Settings,
                                                                _MediaDownloader,
                                                                _MediaLibrary)
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

    }
}
