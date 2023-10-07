using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayerLib.Services.Database.Models
{
    public enum LogEntryType { Info, Error }
    public class LogEntry: BaseDataModel
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public LogEntryType Type { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string Category { get; set; }

        public override string ToString()
        {
            return $"{CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")}: {Type}: {Category} - {Message}";
        }
    }
}
