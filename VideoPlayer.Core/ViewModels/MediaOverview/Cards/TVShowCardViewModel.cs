using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
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
        private readonly IMediaCollectionSelector mediaCollectionSelector;

        public TVShowCardViewModel(
            IPlaylistManager playlistManager, 
            IEnvironment environment, 
            IResourceManager resourceManager,
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            IMediaCollectionSelector mediaCollectionSelector,
            TVShow entry) 
            : base(playlistManager, environment, resourceManager, downloadManager, mediaLibrary, entry)
        {
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
            : base(playlistManager, environment, resourceManager, downloadManager, mediaLibrary, entry)
        {
            this.mediaCollectionSelector = mediaCollectionSelector;
            CollectionContext.Items.Add(new TVShowEpisodeMediaListItem(entry));
        }
        protected override void ExecuteAction(string args)
        {
            try
            {
                switch ((string)args)
                {
                    case "downloadseason":
                        StartDownload(SelectedSeason);
                        break;
                    default:
                        base.ExecuteAction(args);
                        break;
                }
            }
            catch (Exception ex)
            {
                NotifyError(ex);
            }
        }

        public ObservableCollection<TVShowSeason> Seasons { get; } = new ObservableCollection<TVShowSeason>();
        protected TVShow Show { get => base.Entry as TVShow; }
        protected TVShowEpisode Episode { get => base.Entry as TVShowEpisode; }
        protected TVShowEpisode SelectedEpisode
        {
            get => GetProperty<TVShowEpisode>();
            set
            {
                var old = SelectedEpisode;
                if (old is not null)
                    old.PropertyChanged -= SelectedEntry_PropertyChanged;
                SetProperty(value);
                if (value is not null)
                    value.PropertyChanged += SelectedEntry_PropertyChanged;
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
                var old = SelectedShow;
                if (old is not null)
                    old.PropertyChanged -= SelectedEntry_PropertyChanged;
                SetProperty(value);
                if (value is not null)
                    value.PropertyChanged += SelectedEntry_PropertyChanged;
                LoadShowSeasons(value);
            }
        }
        public TVShowSeason SelectedSeason
        {
            get => GetProperty<TVShowSeason>();
            set
            {
                var old = SelectedSeason;
                if (old is not null)
                    old.PropertyChanged -= SelectedEntry_PropertyChanged;
                SetProperty(value);
                if (value is not null)
                    value.PropertyChanged += SelectedEntry_PropertyChanged;
                LoadSeason(value);
            }
        }

        private void SelectedEntry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateMediaInformation(SelectedEpisode ?? SelectedSeason ?? SelectedShow ?? Entry);
        }
        protected override void RemoveDownload(ClassifiedEntry entry)
        {
            entry = SelectedEpisode ?? SelectedSeason ?? SelectedShow ?? entry;
            base.RemoveDownload(entry);
        }
        protected override void Download(ClassifiedEntry entry)
        {
            entry = SelectedEpisode ?? SelectedSeason ?? SelectedShow ?? entry;
            base.Download(entry);
        }
        protected override void UpdateMediaInformation(ClassifiedEntry entry)
        {
            entry = SelectedEpisode ?? SelectedSeason ?? SelectedShow ?? entry;
            base.UpdateMediaInformation(entry);
            if (SelectedEpisode is not null)
            {
                Title = SelectedEpisode.ShowName;
                Name = $"{SelectedEpisode.Episode}: {SelectedEpisode.Name}";
            }
        }
        protected override void Rescan(ClassifiedEntry entry)
        {
            entry = SelectedEpisode ?? SelectedSeason ?? SelectedShow ?? entry;
            base.Rescan(entry);
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
            var seasons = MediaLibrary.GetSeasons(show.Id).OrderBy(s => s.Number).ThenBy(s => s.ReleaseDate).ThenBy(s => s.Name);
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
            var season = value ?? MediaLibrary.GetTVShowSeason(Episode.SeasonId);
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
                var season = MediaLibrary.GetTVShowSeason(Episode.SeasonId);
                var show = MediaLibrary.GetTVShow(season.ShowId);
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
