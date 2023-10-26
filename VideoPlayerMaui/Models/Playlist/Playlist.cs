using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Models.Playlist
{
    public enum PlaylistType
    {

        General,
        User

    }

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

    }
}
