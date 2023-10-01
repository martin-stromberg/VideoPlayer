using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoMeister.Services.Models
{
    public class LibraryMediaItem : MediaItem
    {
        public LibraryMediaItem(Database.Models.MediaItem item, MediaItemCollection collection) 
            : base(collection)
        {
            Name = item.Name;
            URI = item.Path;            
        }

        public override string URI { get; }

        public override string Name { get; }
    }
}
