using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Attributes;

namespace VideoPlayer.Service.Database.Models
{
    [SkipExport]
    public class DataLogEntry: BaseDataModel
    {
        public string Message { get; set; }
        public LogLevel Level { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
