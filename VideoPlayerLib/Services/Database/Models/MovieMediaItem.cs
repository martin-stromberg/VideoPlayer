using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayerLib.Services.Database.Models
{
    public class MovieMediaItem: BaseDataModel
    {
        public long MovieId { get; set; }
        public long MediaItemId { get; set; }
    }
}
