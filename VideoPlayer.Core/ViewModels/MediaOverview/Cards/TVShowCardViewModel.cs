using System.Collections.ObjectModel;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.MediaOverview.Cards
{
    public class TVShowCardViewModel: BaseMediaItemCardViewModel
    {
        private readonly IMediaLibrary mediaLibrary;
        private readonly IMediaCollectionSelector mediaCollectionSelector;

        public TVShowCardViewModel(
            IPlaylistManager playlistManager, 
            IEnvironment environment, 
            IResourceManager resourceManager,
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            IMediaCollectionSelector mediaCollectionSelector,
            TVShow entry) 
            : base(playlistManager, environment, resourceManager, downloadManager, entry)
        {
            this.mediaLibrary = mediaLibrary;
            this.mediaCollectionSelector = mediaCollectionSelector;
            CollectionContext.Items.Add(new TVShowMediaListItem(entry));
            Year = entry.ReleaseDate.Year;
            if (Year == 0)
                Year = entry.PremieredAt.Year;
            Genres = "";
            Plot = entry.Plot;            
        }
        public ObservableCollection<TVShowSeason> Seasons { get; } = new ObservableCollection<TVShowSeason>();
        protected TVShow Show { get => base.Entry as TVShow; }
        protected TVShowEpisode Episode { get => base.Entry as TVShowEpisode; }
        protected TVShowEpisode SelectedEpisode
        {
            get => GetProperty<TVShowEpisode>();
            set
            {
                SetProperty(value);
                VideoSource = null;
            }
        }
        protected TVShow SelectedShow
        {
            get => GetProperty<TVShow>();
            set
            {
                SetProperty(value);
                LoadShowSeasons(value);
            }
        }
        protected TVShowSeason SelectedSeason
        {
            get => GetProperty<TVShowSeason>();
            set
            {
                SetProperty(value);
                LoadSeason(value);
            }
        }

        private void LoadShowSeasons(TVShow show)
        {
            Seasons.Clear();
            var seasons = mediaLibrary.GetSeasons(show.Id);            
            foreach (var season in seasons)
                Seasons.Add(season);
            if (Episode is not null)
                SelectedSeason = Seasons.FirstOrDefault(s => s.Id == Episode.SeasonId);
            else if (Show is not null)
                SelectedSeason = Seasons.FirstOrDefault();
        }

        

        private void LoadSeason(TVShowSeason value)
        {
            if (SelectedShow is null)
                LoadShow();
            var season = mediaLibrary.GetTVShowSeason(Episode.SeasonId);
            var episodes = mediaCollectionSelector.FindNextEntries(season);
            CollectionContext.Items.Clear();
            foreach (var episode in episodes)
                CollectionContext.Items.Add(new TVShowEpisodeMediaListItem(episode));
        }

        private void LoadShow()
        {
            if (Show is not null)
                SelectedShow = Show;
            else if (Episode is not null)
            {
                var season = mediaLibrary.GetTVShowSeason(Episode.SeasonId);
                var show = mediaLibrary.GetTVShow(season.ShowId);
                SelectedShow = show;
            }
        }

        protected override void Select(ClassifiedEntry item)
        {
            SelectedEpisode = item as TVShowEpisode;
            base.Select(item);
        }

        protected override void SetCollectionVisible(bool visible)
        {
            base.SetCollectionVisible(SelectedSeason is not null && visible);
        }
        protected override void ExecuteFirstAppeared()
        {
            base.ExecuteFirstAppeared();
            if (Episode is not null)
                Select(Episode);
        }
    }
}
