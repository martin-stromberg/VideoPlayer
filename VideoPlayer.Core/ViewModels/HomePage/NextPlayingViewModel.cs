using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.Service.Playlists;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.HomePage
{
    public class NextPlayingViewModel : BaseViewModel
    {
        private readonly IPlaylistManager playlistManager;
        private readonly INavigationManager navigationManager;
        private readonly NextPlaybackPlaylist nextPlaybackPlaylist;
        private Playlist playlist;

        public NextPlayingViewModel(
            IPlaylistManager playlistManager,
            INavigationManager navigationManager)
            :base()
        {
            this.playlistManager = playlistManager;
            this.navigationManager = navigationManager;
            this.nextPlaybackPlaylist = playlistManager.NextPlaybackPlaylist;
            this.nextPlaybackPlaylist.PlaylistLoaded += Playlist_PlaylistLoaded;
            Items.CollectionChanged += Items_CollectionChanged1;
        }

        protected override void ExecuteFirstAppeared()
        {
            base.ExecuteFirstAppeared();
            playlistManager.Init();
        }

        private void Items_CollectionChanged1(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            HasItems = Items.Any();
        }
        public bool HasItems { get => GetProperty<bool>(); set => SetProperty(value); }

        private void Playlist_PlaylistLoaded(object sender, Service.Library.Models.BaseServiceModelEventArgs e)
        {
            this.playlist = e.ModelObject as Playlist;
            foreach (var item in playlist.Items)
                AddItem(item);
            playlist.Items.CollectionChanged += PlaylistItems_CollectionChanged;
            
        }

        private void PlaylistItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
                foreach (var item in e.NewItems)
                    AddItem(item as PlaylistEntry);
            if (e.OldItems is not null)
                foreach (var item in e.OldItems)
                    RemoveItem(item as PlaylistEntry);
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
                Items.Move(Items.IndexOf(existing), offset);
            else
            {
                var vm = item.Entry?.Type switch
                {
                    Service.Library.Models.Classified.EntryType.Movie => new MovieMediaListItem(item.Entry),
                    Service.Library.Models.Classified.EntryType.TVShowEpisode => new TVShowEpisodeMediaListItem(item.Entry),
                    _ => new BaseMediaListItem(item.Entry),
                };
                vm.Selected += Item_Selected;
                if (vm is not null)
                    Items.Insert(offset, vm);
            }

        }

        private void Item_Selected(object sender, EventArgs e)
        {
            try
            {
                var vm = (BaseMediaListItem)sender;
                navigationManager.OpenCard(vm);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public ObservableCollection<BaseMediaListItem> Items { get; } = new ObservableCollection<BaseMediaListItem>();
    }
}
