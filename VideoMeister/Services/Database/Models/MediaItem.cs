using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoMeister.Services.Database.Models
{
    public enum MediaItemType { Source, Folder, File }
    public class MediaItem
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public MediaItemType Type { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public long SourceId { get; set; }
        public long ParentId { get; set; }
        public long AlternateId { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null) return this == null;
            if (!(obj is MediaItem)) return false;

            MediaItem other = obj as MediaItem;
            if (other.Type != this.Type) return false;
            if (this.Path != other.Path) return false;
            if (this.SourceId != other.SourceId) return false;
            if (this.ParentId != other.ParentId) return false;

            return true;
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
