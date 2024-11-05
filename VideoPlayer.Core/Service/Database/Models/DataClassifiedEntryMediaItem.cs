using System;
using System.Linq;

namespace VideoPlayer.Service.Database.Models
{
    public class DataClassifiedEntryMediaItem: BaseDataModel
    {

        public long EntryId { get; set; }

        public long MediaItemId { get; set; }

    }
}
