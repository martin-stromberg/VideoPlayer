using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public class PlaylistEntry: BaseDataModel
    {

        public long PlaylistId { get; set; }

        public long MediaItemId { get; set; }

    }
}
