using Microsoft.Extensions.Logging;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Log
{
    [DataModelReference(typeof(DataLogEntry))]
    public class LogEntry : BaseServiceModel
    {
        public LogEntry(BaseDataModel dataModel) 
            : base(dataModel)
        {
            if (DataModel is not null)
            {
                Message = ((DataLogEntry)DataModel).Message;
                Level = ((DataLogEntry)DataModel).Level;
                Timestamp = ((DataLogEntry)DataModel).Timestamp;
            }
        }

        public string Message { get; set; }
        public LogLevel Level { get; set; }
        public DateTime Timestamp { get; set; }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((DataLogEntry)DataModel).Message = Message;
                ((DataLogEntry)DataModel).Level = Level;
                ((DataLogEntry)DataModel).Timestamp = Timestamp;
            }
        }
    }
}
