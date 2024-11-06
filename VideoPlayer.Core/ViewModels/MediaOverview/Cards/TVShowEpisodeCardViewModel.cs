using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.MediaOverview.Cards
{
    [Obsolete($"Use {nameof(TVShowCardViewModel)} instead")]
    public class TVShowEpisodeCardViewModel : BaseMediaItemCardViewModel
    {
        public TVShowEpisodeCardViewModel(IPlaylistManager playlistManager, IEnvironment environment, IResourceManager resourceManager, IDownloadManager downloadManager, TVShowEpisode entry) 
            : base(playlistManager, environment, resourceManager, downloadManager, entry)
        {
            CollectionContext.Items.Add(new TVShowEpisodeMediaListItem(entry));
            Year = entry.ReleaseDate.Year;
            if (Year == 0)
                Year = entry.PremieredAt.Year;
            Genres = "";
            Plot = entry.Plot;
        }
        protected TVShowEpisode Episode { get => base.Entry as TVShowEpisode; }
        
        protected override void SetCollectionVisible(bool visible)
        {
            base.SetCollectionVisible(false);
        }
    }
}
