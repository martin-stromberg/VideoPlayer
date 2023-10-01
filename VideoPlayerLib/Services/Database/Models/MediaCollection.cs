using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayerLib.Services.Database.Models
{
    public class MediaCollection : MediaItem
    {
        public long MediaSourceId { get; set; }
    }
}
