using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(DataProtocolEntry))]
    public class ProtocolEntry : BaseServiceModel
    {
        public ProtocolEntry(BaseDataModel dataModel) 
            : base(dataModel)
        {
            if(dataModel is not null)
            {
                EntryType = ((DataProtocolEntry)dataModel).EntryType;
                Description = ((DataProtocolEntry)dataModel).Description;
                EntryId = ((DataProtocolEntry)dataModel).EntryId;
            }
        }
        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((DataProtocolEntry)DataModel).EntryType = EntryType;
                ((DataProtocolEntry)DataModel).Description = Description;
                ((DataProtocolEntry)DataModel).EntryId = EntryId;
            }
        }
        public string EntryType { get; private set; }
        public string Description { get; private set; }
        public long EntryId { get; private set; }
    }
}
