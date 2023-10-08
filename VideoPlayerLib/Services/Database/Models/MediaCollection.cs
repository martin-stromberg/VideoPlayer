using System;
using System.Linq;

namespace VideoPlayerLib.Services.Database.Models
{
    public class MediaCollection : MediaItem
    {
        public long MediaSourceId { get; set; }
    }
}
