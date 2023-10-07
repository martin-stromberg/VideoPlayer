using MyVideoPlayer.Helper.LibraryScan;
using MyVideoPlayer.ViewModels.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;

namespace MyVideoPlayer.ViewModels.Logs
{
    internal class LogEntryBoxViewModel: BaseMediaElementBoxViewModel
    {
        private LogEntry entry;

        public LogEntryBoxViewModel(LogEntry entry) : base(null)
        {
            this.entry = entry;
        }

        public override string ToString()
        {
            return entry.ToString();
        }
    }
}
