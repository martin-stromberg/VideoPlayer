using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.Playlists
{
    public enum PlaylistType
    {

        General,
        User

    }

    [DataModelReference(typeof(Services.Database.Models.Playlist))]
    public class Playlist: BaseModel
    {

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

        public ObservableCollection<PlaylistEntry> Items { get; } = new ObservableCollection<PlaylistEntry>();

        public void Add(PlaylistEntry entry)
        {
            entry.PlaylistId = Id;
            Items.Add(entry);
        }

        public void Add(MediaItem entry)
        {
            Add(new PlaylistEntry() { Item = entry });
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

        private void Remove(PlaylistEntry listItem)
        {
            Items.Remove(listItem);
        }

    }
}
