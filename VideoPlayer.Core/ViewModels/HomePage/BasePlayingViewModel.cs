using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.HomePage
{
    public class BasePlayingViewModel : BaseViewModel
    {
        //private readonly IPlaylistManager playlistManager;
        private readonly INavigationManager navigationManager;
        private readonly IResourceManager resourceManager;
        private readonly BasePlaylistService nextPlaybackPlaylist;
        private Playlist playlist;

        public BasePlayingViewModel(
            BasePlaylistService playlistService,
            INavigationManager navigationManager,
            IResourceManager resourceManager,
            ILogger logger)
            :base(logger)
        {
            //this.playlistManager = playlistManager;
            this.navigationManager = navigationManager;
            this.resourceManager = resourceManager;
            this.nextPlaybackPlaylist = playlistService;
            Playlist_PlaylistLoaded(this, new BaseServiceModelEventArgs(nextPlaybackPlaylist.Current));
            this.nextPlaybackPlaylist.PlaylistLoaded += Playlist_PlaylistLoaded;
            Items.CollectionChanged += Items_CollectionChanged1;
        }

        private void Items_CollectionChanged1(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            HasItems = Items.Any();
        }
        public bool HasItems { get => GetProperty<bool>(); set => SetProperty(value); }
        public bool AllowAutoPlay 
        { 
            get => GetProperty<bool>(); 
            set 
            {
                SetProperty(value);
                foreach (var item in Items)
                    item.AllowAutoPlay = value;
            }
        }
        private void Playlist_PlaylistLoaded(object sender, Service.Library.Models.BaseServiceModelEventArgs e)
        {
            this.playlist = e.ModelObject as Playlist;
            if (playlist is null)
                return;
            Items.Clear();
            foreach (var item in playlist.Items)
                AddItem(item);
            HasItems = Items.Any();
            playlist.Items.CollectionChanged += PlaylistItems_CollectionChanged;            
        }

        private void PlaylistItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (e.NewItems is not null)
                    foreach (var item in e.NewItems)
                        AddItem(item as PlaylistEntry);
                if (e.OldItems is not null)
                    foreach (var item in e.OldItems)
                        RemoveItem(item as PlaylistEntry);
            });
        }

        private void RemoveItem(PlaylistEntry item)
        {
            var existing = Items.FirstOrDefault(i => i.Id == item.EntryId);
            if (existing is not null)
            {
                existing.Selected -= Item_Selected;
                Items.Remove(existing);
            }
        }

        private void AddItem(PlaylistEntry item)
        {
            var offset = playlist.Items.IndexOf(item);
            var existing = Items.FirstOrDefault(i => i.Id == item.EntryId);
            if (existing is not null)
            {
                if (offset >= Items.Count)
                {
                    Items.Remove(existing);
                    Items.Append(existing);
                }
                else
                Items.Move(Items.IndexOf(existing), offset);
            }
            else
            {
                var vm = item.Entry?.Type switch
                {
                    Service.Library.Models.Classified.EntryType.MovieCollection => new MovieCollectionMediaListItem(item.Entry, resourceManager, Logger)
                    {
                        ApplicationArea = CardItemApplicationArea.Single,
                        AllowAutoPlay = AllowAutoPlay
                    },
                    Service.Library.Models.Classified.EntryType.Movie => new MovieMediaListItem(item.Entry, resourceManager, Logger)
                    {
                        ApplicationArea = CardItemApplicationArea.Single,
                        AllowAutoPlay = AllowAutoPlay
                    },
                    Service.Library.Models.Classified.EntryType.TVShowEpisode => new TVShowEpisodeMediaListItem(item.Entry, resourceManager, Logger)
                    {
                        ApplicationArea = CardItemApplicationArea.Single,
                        AllowAutoPlay = AllowAutoPlay
                    },
                    Service.Library.Models.Classified.EntryType.TVShowSeason => new TVShowSeasonMediaListItem(item.Entry, resourceManager, Logger)
                    {
                        ApplicationArea = CardItemApplicationArea.Single,
                        AllowAutoPlay = AllowAutoPlay
                    },
                    Service.Library.Models.Classified.EntryType.TVShow => new TVShowMediaListItem(item.Entry, resourceManager, Logger)
                    {
                        ApplicationArea = CardItemApplicationArea.Single,
                        AllowAutoPlay = AllowAutoPlay
                    },
                    _ => new BaseMediaListItem(item.Entry, resourceManager, Logger)
                    {
                        ApplicationArea = CardItemApplicationArea.Single,
                        AllowAutoPlay = AllowAutoPlay
                    },
                };
                vm.Selected += Item_Selected;
                if (vm is not null)
                    Items.Insert(Math.Min(offset, Items.Count), vm);
            }

        }

        private void Item_Selected(object sender, ArgumentEventArgs e)
        {
            try
            {
                var vm = (BaseMediaListItem)sender;
                navigationManager.OpenCard(vm, (bool)e.Argument);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public ObservableCollection<BaseMediaListItem> Items { get; } = new ObservableCollection<BaseMediaListItem>();
    }
}
