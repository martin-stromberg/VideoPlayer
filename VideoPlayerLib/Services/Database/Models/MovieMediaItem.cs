using System;
using System.Linq;

namespace VideoPlayerLib.Services.Database.Models
{
    public class MovieMediaItem : BaseDataModel
    {
        public long MovieId { get; set; }
        public long MediaItemId { get; set; }
    }
}
