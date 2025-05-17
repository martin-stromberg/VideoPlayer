using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library.Models.Classified;

namespace VideoPlayer.Service.Library.Models.Playlists
{
    [DataModelReference(typeof(Service.Database.Models.DataPlaylistEntry))]
    public class PlaylistEntry: BaseServiceModel
    {
        public PlaylistEntry(BaseDataModel dataModel) 
            : base(dataModel)
        {
            if (DataModel is not null)
            {
                PlaylistId = ((DataPlaylistEntry)DataModel).PlaylistId;
                MediaItemId = ((DataPlaylistEntry)DataModel).MediaItemId;
                EntryId = ((DataPlaylistEntry)DataModel).EntryId;
            }
        }
        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((DataPlaylistEntry)DataModel).PlaylistId = PlaylistId;
                ((DataPlaylistEntry)DataModel).MediaItemId = MediaItemId;
                ((DataPlaylistEntry)DataModel).EntryId = EntryId;
            }
        }
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
        public long EntryId
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

        public ClassifiedEntry Entry {
            get
            {
                return GetProperty<ClassifiedEntry>();
            }
            set
            {
                SetProperty<ClassifiedEntry>(value);
                EntryId = (value == null) ? 0 : value.Id;
            }
        }

        public string Tenant { get { return Item != null ? Item.Tenant : Entry != null ? Entry.Tenant : string.Empty; } }
    }
}
