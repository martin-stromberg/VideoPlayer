using Mediathek.Services.Database;
using System;
using System.Linq;

namespace Mediathek.Models.Playlists
{
    [DataModelReference(typeof(Services.Database.Models.PlaylistEntry))]
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
