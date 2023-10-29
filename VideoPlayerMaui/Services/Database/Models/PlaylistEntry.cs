using System;
using System.Linq;

namespace VideoPlayer.Services.Database.Models
{
    public class PlaylistEntry: BaseDataModel
    {

        public long PlaylistId { get; set; }

        public long MediaItemId { get; set; }

    }
}
