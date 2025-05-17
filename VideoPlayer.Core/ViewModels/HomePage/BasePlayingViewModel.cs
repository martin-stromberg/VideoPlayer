using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.Service.Library.Tenants;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.HomePage
{
    public class BasePlayingViewModel : BaseViewModel
    {
        private readonly ITenantSelection tenantSelection;

        //private readonly IPlaylistManager playlistManager;
        private readonly INavigationManager navigationManager;
        private readonly IResourceManager resourceManager;
        private readonly BasePlaylistService nextPlaybackPlaylist;
        private Playlist playlist;

        public BasePlayingViewModel(
            BasePlaylistService playlistService,
            ITenantSelection tenantSelection,
            INavigationManager navigationManager,
            IResourceManager resourceManager,
            ILogger logger)
            :base(logger)
        {
            //this.playlistManager = playlistManager;
            this.tenantSelection = tenantSelection;
            this.tenantSelection.TenantChanged += TenantSelection_TenantChanged;
            this.navigationManager = navigationManager;
            this.resourceManager = resourceManager;
            this.nextPlaybackPlaylist = playlistService;
            Playlist_PlaylistLoaded(this, new BaseServiceModelEventArgs(nextPlaybackPlaylist.Current));
            this.nextPlaybackPlaylist.PlaylistLoaded += Playlist_PlaylistLoaded;
            AllItems.CollectionChanged += Items_CollectionChanged1;
        }

        private void TenantSelection_TenantChanged(object sender, string e)
        {
            TenantChanged(e);
        }
        protected virtual void TenantChanged(string tenant)
        {
            UpdateTenantItems();
            OnPropertyChanged(nameof(CurrentTenant));
        }
        protected string CurrentTenant
        {
            get { return tenantSelection.CurrentTenant; }
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
            AllItems.Clear();
            Items.Clear();
            foreach (var item in playlist.Items.Where(i => i.Entry is not null || i.Item is not null))
                AddItem(item);
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
            existing = AllItems.FirstOrDefault(i => i.Id == item.EntryId);
            if (existing is not null)
                AllItems.Remove(existing);
        }

        private void AddItem(PlaylistEntry item)
        {
            var offset = playlist.Items.IndexOf(item);
            var existing = AllItems.FirstOrDefault(i => i.Id == item.EntryId);
            if (existing is not null)
            {
                if (offset >= AllItems.Count)
                {
                    AllItems.Remove(existing);
                    AllItems.Append(existing);
                }
                else
                    AllItems.Move(AllItems.IndexOf(existing), offset);
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
                if (vm is not null)
                    AllItems.Insert(Math.Min(offset, AllItems.Count), vm);
            }
            UpdateTenantItems();
        }

        private void UpdateTenantItems()
        {
            var itemList = AllItems.Where(i => {
                var entry = i.Item as ClassifiedEntry;
                if (entry is not null)
                    return (entry.Tenant == CurrentTenant) || (string.IsNullOrWhiteSpace(entry.Tenant) && CurrentTenant == "Standard");
                entry = i.Element as ClassifiedEntry;
                if (entry is not null)
                    return (entry.Tenant == CurrentTenant) || (string.IsNullOrWhiteSpace(entry.Tenant) && CurrentTenant == "Standard");
                return false;
            }).ToArray();
            for (int idx = itemList.GetLowerBound(0); idx <= itemList.GetUpperBound(0); idx++)
            {
                var item = itemList[idx];                
                var current = Items.Skip(idx).FirstOrDefault();
                if (current is not null && current.Id == item.Id)
                    continue;
                var existing = Items.FirstOrDefault(i => i.Id == item.Id);
                if (existing is not null)
                    Items.Move(Items.IndexOf(existing), idx);
                else
                {
                    item.Selected += Item_Selected;
                    Items.Insert(idx, item);
                }
            }
            while (Items.Count() > itemList.Count())
            {
                var item = Items.Last();
                item.Selected -= Item_Selected;
                Items.Remove(item);
            }
            HasItems = Items.Any();
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
        private ObservableCollection<BaseMediaListItem> AllItems { get; } = new ObservableCollection<BaseMediaListItem>();
    }
}
