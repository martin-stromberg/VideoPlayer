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
        protected Movie EffectiveSelectedMovie { get => GetProperty<Movie>(); set => SetProperty(value); }
        protected Movie SelectedMovie
        {
            get { return GetProperty<Movie>(); }
            set { SetProperty(value); if (!IsAppearing) EffectiveSelectedMovie = value; VideoSource = null; }
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
        protected override void OpenProtocol(ClassifiedEntry entry)
        {
            entry = EffectiveSelectedMovie ?? entry;
            base.OpenProtocol(entry);
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
            await Task.Run(() => { LoadActors(Entry); });
                   
        }
        public ObservableCollection<RoleListItem> Roles { get; } = new ObservableCollection<RoleListItem>();
        private async void LoadActors(ClassifiedEntry entry)
        {
            try
            {
                entry = SelectedMovie ?? entry;
                if (entry is MovieCollection)
                {
                    Dictionary<long, List<Role>> actorCollections = new Dictionary<long, List<Role>>();
                    foreach (var item in CollectionContext.Items)
                        actorCollections.Add(item.Id, new List<Role>());
                    int offset = 0;
                    int count = 10;
                    var totalCount = 0;
                    var collectionChanged = true;
                    var isFirst = true;
                    while (collectionChanged)
                    {
                        foreach (var item in CollectionContext.Items)
                            actorCollections[item.Id].AddRange(MediaLibrary.GetRoles(item.Id, offset, count));
                        var newtotalCount = actorCollections.Sum(e => e.Value.Count());
                        collectionChanged = newtotalCount != totalCount;
                        totalCount = newtotalCount;
                        var roles = actorCollections.Values.SelectMany(e => e)

                            .GroupBy(e => e.ActorId)
                            .Where(e =>
                            {
                                var result = e.Count() == CollectionContext.Items.Count;
                                return result;
                            })
                            .Select(e =>
                            {
                                return e.First();
                            })
                            .OrderByDescending(role => role.Actor.RoleCount)
                            .ToArray();
                        if (roles.Any())
                        {
                            await MainThread.InvokeOnMainThreadAsync(() => { UpdateRoles(roles, isFirst); });
                            isFirst = false;
                            foreach (var roleToRemove in roles)
                                foreach (var item in CollectionContext.Items)
                                {
                                    var existing = actorCollections[item.Id].FirstOrDefault(e => e.Id == roleToRemove.Id);
                                    if (existing is null)
                                        existing = actorCollections[item.Id].FirstOrDefault(e => e.ActorId == roleToRemove.ActorId);
                                    if (existing is not null)
                                        actorCollections[item.Id].Remove(existing);
                                }
                        }
                    }
                    foreach (var list in actorCollections.Values)
                        MediaLibrary.Release(list);
                    actorCollections.Clear();
                }
                else
                {
                    int offset = 0;
                    int count = 10;
                    var roles = MediaLibrary.GetRoles(entry.Id, offset, count)
                        .Select(role => role)
                        .ToArray();
                    while (roles.Any())
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => { UpdateRoles(roles, offset == 0); }).ConfigureAwait(false);
                        offset += roles.Count();
                        count = 1;
                        await Task.Delay(100);
                        roles = await Task<Role[]>.Run(() => { 
                            return MediaLibrary.GetRoles(entry.Id, offset, count)
                                .Select(role => role)
                                .ToArray();
                            });
                    }
                }
            }
            catch (Exception ex) 
            {
                NotifyError(ex);
            }
        }

        private void UpdateRoles(Role[] roles, bool clearFirst)
        {            
            if (clearFirst)
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
