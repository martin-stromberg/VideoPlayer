using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Service.Database.Models
{
    public class DataRole: BaseDataModel
    {
        public long EntryId { get => GetProperty<long>(); set => SetProperty(value); }
        public long ActorId { get => GetProperty<long>(); set => SetProperty(value); }
        public long Order { get => GetProperty<long>(); set => SetProperty(value); }
    }
}
