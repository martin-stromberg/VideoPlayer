using System;
using System.Linq;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.Playlists
{
    [DataModelReference(typeof(VideoPlayer.Services.Database.Models.PlaylistEntry))]
    public class PlaylistEntry: BaseModel
    {

        public long PlaylistId
        {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public long MediaItemId
        {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public MediaItem Item
        {
            get
            {
                return GetProperty<MediaItem>();
            }
            set
            {
                SetProperty<MediaItem>(value);
                MediaItemId = (value == null) ? 0 : value.Id;
            }
        }

    }
}
