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
                       
        }
        public TVShowCardViewModel(
            IPlaylistManager playlistManager, 
            IEnvironment environment, 
            IResourceManager resourceManager, 
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary,
            IMediaCollectionSelector mediaCollectionSelector,
            TVShowEpisode entry)
            : base(playlistManager, environment, resourceManager, downloadManager, entry)
        {
            this.mediaLibrary = mediaLibrary;
            this.mediaCollectionSelector = mediaCollectionSelector;
            CollectionContext.Items.Add(new TVShowEpisodeMediaListItem(entry));
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
                if (value is not null && SelectedSeason is null || SelectedSeason.Id != value.SeasonId)
                    LoadSeason(null);
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
        public TVShowSeason SelectedSeason
        {
            get => GetProperty<TVShowSeason>();
            set
            {
                SetProperty(value);
                LoadSeason(value);
            }
        }

        protected override void UpdateMediaInformation(ClassifiedEntry entry)
        {
            entry = SelectedEpisode ?? SelectedSeason ?? SelectedShow ?? entry;
            base.UpdateMediaInformation(entry);
            if (SelectedEpisode is not null)
                Title = $"{SelectedEpisode.Episode}: {SelectedEpisode.Name}";
        }

        protected override void ExecutePlaybackCommand()
        {
            if (SelectedEpisode is null)
                base.ExecutePlaybackCommand();
            else
            {
                PlayLoadingVideo();
                StartPlayback(SelectedEpisode);
            }
        }

        private void LoadShowSeasons(TVShow show)
        {
            Seasons.Clear();
            var seasons = mediaLibrary.GetSeasons(show.Id).OrderBy(s => s.Number).ThenBy(s => s.ReleaseDate).ThenBy(s => s.Name);
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
            var season = value ?? mediaLibrary.GetTVShowSeason(Episode.SeasonId);
            var episodes = mediaCollectionSelector.FindNextEntries(season).Cast<TVShowEpisode>().Where(e => e.SeasonId == season.Id);
            CollectionContext.Items.Clear();
            foreach (var episode in episodes)
                CollectionContext.Items.Add(new TVShowEpisodeMediaListItem(season, episode));
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
            else
                LoadShow();
        }
    }
}
