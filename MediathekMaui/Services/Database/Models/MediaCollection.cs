using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public class MediaCollection: MediaItem
    {

        public long MediaSourceId { get; set; }

        public DateTime LastUpdate { get; set; }

    }
}
