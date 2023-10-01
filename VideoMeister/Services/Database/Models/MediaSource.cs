using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoMeister.Services.Database.Models
{
    public class MediaSource
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Configuration { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null) return this == null;
            if (!(obj is MediaSource)) return false;

            MediaSource other = obj as MediaSource;
            if (other.Type != this.Type) return false;
            if (this.Configuration != other.Configuration) return false;

            return true;
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
