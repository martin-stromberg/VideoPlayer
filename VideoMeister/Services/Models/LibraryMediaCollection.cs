using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.VideoSources;

namespace VideoMeister.Services.Models
{
    public class LibraryMediaCollection : MediaItemCollection
    {
        public LibraryMediaCollection(VideoSource source) 
            : base(source)
        {
            Name = source.Name;
        }

        public override MediaItemCollection[] Folders => throw new NotImplementedException();

        public override MediaItem[] Files => throw new NotImplementedException();

        public override string Name { get; }

        public override DriveMediaItemCollection CreateFolder(string name)
        {
            throw new NotImplementedException();
        }

        public override void Refresh()
        {
            throw new NotImplementedException();
        }
    }
}
