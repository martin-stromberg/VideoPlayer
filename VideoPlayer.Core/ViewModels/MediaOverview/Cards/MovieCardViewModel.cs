using Syncfusion.XlsIO.FormatParser.FormatTokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Device;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
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
            INavigationManager navigationManager,
            Movie entry)
            :base(playlistManager, environment, resourceManager, downloadManager, mediaLibrary, navigationManager, entry)
        {
            CollectionContext.Items.Add(new MovieMediaListItem(entry, resourceManager));
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
            INavigationManager navigationManager,
            MovieCollection entry)
            : base(playlistManager, environment, resourceManager, downloadManager, mediaLibrary, navigationManager, entry)
        {
            CollectionContext.Items.Add(new MovieCollectionMediaListItem(entry, resourceManager));            
            this.mediaCollectionSelector = mediaCollectionSelector;
            Select(null);
        }
        protected override void Download(ClassifiedEntry entry)
        {
            entry = SelectedMovie ?? entry;
            base.Download(entry);
        }
        protected override void RemoveDownload(ClassifiedEntry entry)
        {
            entry = SelectedMovie ?? entry;
            base.RemoveDownload(entry);
        }
        protected override void UpdateMediaInformation(ClassifiedEntry entry)
        {
            entry = SelectedMovie ?? entry;
            base.UpdateMediaInformation(entry);
        }
        protected override void Rename(ClassifiedEntry entry, string newName)
        {
            entry = SelectedMovie ?? entry;
            if (entry is MovieCollection)
                base.Rename(entry, newName);
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
                    CollectionContext.Items.Add(new MovieMediaListItem(entry, ResourceManager));
                //Select(CollectionContext.Items.FirstOrDefault()?.Element as ClassifiedEntry);
            }  
            else if (Movie is not null && Movie.CollectionId != 0)
            {
                var collection = MediaLibrary.GetMovieCollection(Movie.CollectionId);
                CollectionContext.Items.Clear();
                foreach (var entry in mediaCollectionSelector.FindNextEntries(collection))
                    CollectionContext.Items.Add(new MovieMediaListItem(entry, ResourceManager));

                LoadActorsAsync();
            }
        }

        private async void LoadActorsAsync()
        {
            await Task.Delay(100);
            LoadActors(Entry);            
        }
        public ObservableCollection<RoleListItem> Roles { get; } = new ObservableCollection<RoleListItem>();
        private void LoadActors(ClassifiedEntry entry)
        {
            entry = SelectedMovie ?? entry;
            if (entry is MovieCollection)
            {
                var roles = CollectionContext.Items.SelectMany(e => MediaLibrary.GetRoles(e.Id))
                    .GroupBy(e => e.ActorId)
                    .Where(e =>
                    {
                        var result = e.Count() == CollectionContext.Items.Count;
                        if (!result)
                            MediaLibrary.Release(e.Cast<BaseServiceModel>());
                        return result;
                    })
                    .Select(e =>
                    {
                        MediaLibrary.Release(e.Skip(1));
                        return e.First();
                    })
                    .OrderByDescending(role => role.Actor.RoleCount)
                    .ToArray();
                UpdateRoles(roles);
            }
            else
            {
                var roles = MediaLibrary.GetRoles(entry.Id)
                    .Select(role => role)
                    .OrderByDescending(role => role.Actor.RoleCount)
                    .ToArray();
                UpdateRoles(roles);
            }            
            
        }

        private void UpdateRoles(Role[] roles)
        {
            SecondCollectionContext.Items.Clear();
            foreach (var role in roles)
                SecondCollectionContext.Items.Add(new RoleListItem(role, ResourceManager));
        }

        protected override void SetCollectionVisible(bool visible)
        {            
            base.SetCollectionVisible(visible && CollectionContext.Items.Count > 1);
        }
    }
}
