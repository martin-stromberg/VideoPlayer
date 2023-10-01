using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoMeister.Services.Models
{
    public class DriveMediaItem : MediaItem
    {
        private string path { get; }
        public DriveMediaItem(DriveMediaItemCollection parent, string path) 
            : base(parent)
        {
            this.path = path;
        }

        public override string URI => path;

        public override string Name => new FileInfo(URI).Name;
    }
}
