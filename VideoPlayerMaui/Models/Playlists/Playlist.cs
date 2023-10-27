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

        public ObservableCollection<MediaItem> Items { get; } = new ObservableCollection<MediaItem>();

        public void Add(MediaItem mediaItem)
        {
            Items.Add(mediaItem);
        }

        public void RemoveUpTo(MediaItem item)
        {
            foreach (var listItem in Items.TakeWhile(i => (i.Id != item.Id) && (i.Id != item.OriginalMediaItemId))
                                          .ToArray())
                Remove(listItem);
            foreach (var listItem in Items.TakeWhile(i => (i.Id == item.Id) || (i.Id == item.OriginalMediaItemId))
                                          .ToArray())
                Remove(listItem);
        }

        private void Remove(MediaItem listItem)
        {
            Items.Remove(listItem);
        }

    }
}
