using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library.Models.Classified;
using static VideoPlayer.Service.Download.DownloadManager;

namespace VideoPlayer.Service.Library.Models.Playlists
{
    public enum PlaylistType
    {

        General,
        User,
        TVShowCollection,
        NextPlayback,
        New
    }
    [DataModelReference(typeof(Service.Database.Models.DataPlaylist))]
    public class Playlist : BaseServiceModel
    {
        private readonly IDownloadManager downloadManager;

        public Playlist(BaseDataModel dataModel) 
            : base(dataModel)
        {
            if (DataModel is not null)
            {
                Type = (PlaylistType)((DataPlaylist)DataModel).Type;
                CurrentPosition = ((DataPlaylist)DataModel).CurrentPosition;
                AutoDownload = ((DataPlaylist)DataModel).AutoDownload;
                BagMode = ((DataPlaylist)DataModel).BagMode;
            }
        }
        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((DataPlaylist)DataModel).Type = (DataPlaylist.PlaylistType)Type;
                ((DataPlaylist)DataModel).CurrentPosition = CurrentPosition;
                ((DataPlaylist)DataModel).AutoDownload = AutoDownload;
                ((DataPlaylist)DataModel).BagMode = BagMode;
            }
        }
        public PlaylistEntry First { get => Items.FirstOrDefault(); }
        public ObservableCollection<PlaylistEntry> Items { get; } = new ObservableCollection<PlaylistEntry>();
        public int CurrentPosition { get; internal set; }
        public PlaylistType Type
        {
            get
            {
                return GetProperty<PlaylistType>();
            }
            set
            {
                SetProperty<PlaylistType>(value);
            }
        }

        public bool AutoDownload { get; internal set; }
        public bool BagMode { get; internal set; }
        public DownloadSession CurrentDownload { 
            get => GetProperty<DownloadSession>(); 
            private set
            {
                var old = CurrentDownload;
                if (old is not null)
                {
                    old.Failed -= DownloadSession_Failed;
                    old.Finished -= DownloadSession_Finished;
                }
                SetProperty(value);
                if (value is not null)
                {
                    value.Failed += DownloadSession_Failed;
                    value.Finished += DownloadSession_Finished;
                }
            }
        }
        public event EventHandler<PlaylistEntry> PlaybackRequest;
        private void DownloadSession_Finished(object sender, DownloadEventArgs e)
        {
            if (e.Session != CurrentDownload)
                return;
            var firstEntry = Items.FirstOrDefault();
            if (firstEntry?.Entry.Id != e.ModelObject.Id)
                return;
            firstEntry.Item = e.Session.Item;
            PlaybackRequest?.Invoke(this, firstEntry);
        }
        private void DownloadSession_Failed(object sender, DownloadFailedEventArgs e)
        {
            if (e.Session != CurrentDownload)
                return;
            var firstEntry = Items.FirstOrDefault();
            if (firstEntry?.Entry.Id != e.ModelObject.Id)
                return;
            CurrentDownload = null;
            DownloadFailed?.Invoke(this, e);
        }

        public void Add(PlaylistEntry entry)
        {
            entry.PlaylistId = Id;
            if (BagMode)
                Items.Insert(0, entry);
            else
                Items.Add(entry);
            if (Items.IndexOf(entry) == 0)
                CurrentDownload = OnDownloadRequested(entry);
        }

        public void Add(MediaItem mediaItem, ClassifiedEntry entry)
        {
            Add(new PlaylistEntry(null) { Item = mediaItem, Entry = entry });
        }

        public void RemoveUpTo(MediaItem item)
        {
            foreach (var listItem in Items.TakeWhile(i =>
                                                     (i.Item.Id != item.Id) && (i.Item.Id != item.OriginalMediaItemId))
                                          .ToArray())
                Remove(listItem);
            foreach (var listItem in Items.TakeWhile(i =>
                                                     (i.Item.Id == item.Id) || (i.Item.Id == item.OriginalMediaItemId))
                                          .ToArray())
                Remove(listItem);
        }

        internal void Clear()
        {
            Items.Clear();
        }

        public void Remove(PlaylistEntry listItem)
        {
            var startDownload = Items.IndexOf(listItem) == 0;
            Items.Remove(listItem);
            var entry = Items.FirstOrDefault();
            if (startDownload && entry is not null)
                CurrentDownload = OnDownloadRequested(entry);
        }

        protected DownloadSession OnDownloadRequested(PlaylistEntry e)
        {
            var args = new DownloadEventArgs(e);
            OnDownloadRequested(args);
            return args.Session;
        }
        protected void OnDownloadRequested(DownloadEventArgs e)
        {
            DownloadRequested?.Invoke(this, e);
        }

        internal void MoveTo(PlaylistEntry existing, int v)
        {
            Remove(existing);
            Add(existing);
        }

        public event EventHandler<DownloadEventArgs> DownloadRequested;
        public event EventHandler<DownloadFailedEventArgs> DownloadFailed;
    }
}
