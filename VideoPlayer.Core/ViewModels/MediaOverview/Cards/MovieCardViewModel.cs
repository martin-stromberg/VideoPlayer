using Syncfusion.XlsIO.FormatParser.FormatTokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.MediaOverview.Cards
{
    public class MovieCardViewModel : BaseMediaItemCardViewModel
    {
        private readonly IMediaCollectionSelector mediaCollectionSelector;
        protected Movie Movie { get => base.Entry as Movie; }
        protected MovieCollection Collection { get => base.Entry as MovieCollection; }
        protected Movie SelectedMovie
        {
            get { return GetProperty<Movie>(); }
            set { SetProperty(value); VideoSource = null; }
        }

        public MovieCardViewModel(
            IPlaylistManager playlistManager, 
            IEnvironment environment, 
            IResourceManager resourceManager,
            IMediaCollectionSelector mediaCollectionSelector,
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            Movie entry)
            :base(playlistManager, environment, resourceManager, downloadManager, mediaLibrary, entry)
        {
            CollectionContext.Items.Add(new MovieMediaListItem(entry));
            Year = entry.ReleaseDate.Year;
            if (Year == 0)
                Year = entry.PremieredAt.Year;
            Genres = string.Join(", ", entry.Genres);
            Plot = entry.Plot;
            this.mediaCollectionSelector = mediaCollectionSelector;
        }

        public MovieCardViewModel(
            IPlaylistManager playlistManager, 
            IEnvironment environment, 
            IResourceManager resourceManager,
            IMediaCollectionSelector mediaCollectionSelector,
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            MovieCollection entry)
            : base(playlistManager, environment, resourceManager, downloadManager, mediaLibrary, entry)
        {
            CollectionContext.Items.Add(new MovieCollectionMediaListItem(entry));            
            this.mediaCollectionSelector = mediaCollectionSelector;
            Select(null);
        }
        protected override void UpdateMediaInformation(ClassifiedEntry entry)
        {
            entry = SelectedMovie ?? entry;
            base.UpdateMediaInformation(entry);
        }
        protected override void Rescan(ClassifiedEntry entry)
        {
            entry = SelectedMovie ?? entry;
            base.Rescan(entry);
        }
        protected override void Select(ClassifiedEntry item)
        {
            SelectedMovie = item as Movie;
            base.Select(item);
        }

        protected override void ExecutePlaybackCommand()
        {
            if (SelectedMovie is null)
                base.ExecutePlaybackCommand();
            else
            {
                PlayLoadingVideo();
                StartPlayback(SelectedMovie);
            }
        }

        public override void ExecuteAppeared()
        {            
            base.ExecuteAppeared();
        }

        public override void ExecuteDisappeared()
        {
            base.ExecuteDisappeared();
        }
        protected override void ExecuteFirstAppeared()
        {
            base.ExecuteFirstAppeared();   
            if (Collection is not null)
            {
                CollectionContext.Items.Clear();
                foreach (var entry in mediaCollectionSelector.FindNextEntries(Collection))
                    CollectionContext.Items.Add(new MovieMediaListItem(entry));
                Select(CollectionContext.Items.FirstOrDefault()?.Item);
            }  
            else if (Movie is not null && Movie.CollectionId != 0)
            {
                var collection = MediaLibrary.GetMovieCollection(Movie.CollectionId);
                CollectionContext.Items.Clear();
                foreach (var entry in mediaCollectionSelector.FindNextEntries(collection))
                    CollectionContext.Items.Add(new MovieMediaListItem(entry));
            }
        }
        protected override void SetCollectionVisible(bool visible)
        {            
            base.SetCollectionVisible(visible && CollectionContext.Items.Count > 1);
        }
    }
}
