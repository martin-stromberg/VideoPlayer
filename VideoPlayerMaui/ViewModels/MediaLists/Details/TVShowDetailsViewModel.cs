using System;
using System.Collections.ObjectModel;
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
            for (int idx = 0; idx < 100; idx++)
                Episodes.Add(new TVShowEpisodeListItemViewModel(new TVShowEpisode()
                    {
                        EpisodeNo = "{idx}",
                        Id = 0,
                        MediaItems = new long[] { },
                        SeasonId = 0,
                        SeasonName = ".",
                        Name = ".",
                        ShowName = string.Empty
                    },
                                                                () => null,
                                                                StatusPublisher,
                                                                navigationManager,
                                                                settings,
                                                                mediaDownloader,
                                                                mediaLibrary)
                    {
                        Mode = ItemViewModel.Dummy
                    });
        }

        public void SetParent(TVShow show)
        {
            Show = show;
            Title = show.Name;
            Banner = Show.Banner;
        }

        public TVShow Show { get; private set; }

        private TVShowSeason _SelectedSeason = null;

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
                LoadEpisodes();
            }
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LoadSeasons();
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

        private async void LoadSeasons()
        {
            var currentSeason = SelectedSeason;
            var seasons = await _MediaLibrary.GetTVShowSeasons(Show.Id);
            Seasons.Clear();
            foreach (var season in seasons)
            {
                Seasons.Add(season);
                if ((currentSeason != null) && (season.Id == currentSeason.Id))
                    SelectedSeason = season;
            }
            if (SelectedSeason == null)
                SelectedSeason = Seasons.FirstOrDefault();
        }

        private long loadSessionId = 0;

        private async void LoadEpisodes()
        {
            loadSessionId = DateTime.Now.Ticks;
            await LoadEpisodes(loadSessionId);
        }

        private async Task LoadEpisodes(long sessionId)
        {
            Episodes.Clear();
            if (SelectedSeason == null)
                return;
            if (loadSessionId != sessionId)
                return;
            Banner = SelectedSeason.Banner ?? Show.Banner;
            var episodes = await _MediaLibrary.GetTVShowEpisodes(SelectedSeason.Id);
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
            }
        }

    }
}
