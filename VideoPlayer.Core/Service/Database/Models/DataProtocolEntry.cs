using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Service.Database.Models
{
    public class DataProtocolEntry: BaseDataModel
    {
        public string EntryType { get; set; }
        [Indexed]
        public long EntryId { get; set; }

        public string Description { get; set; }
    }
}
