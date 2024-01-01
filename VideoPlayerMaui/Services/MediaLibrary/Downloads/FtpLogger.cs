using FluentFTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Services.MediaLibrary.Downloads
{
    internal class FtpLogger : IFtpLogger
    {
        public void Log(FtpLogEntry entry)
        {
            NewEntry?.Invoke(this, entry);
        }

        public event EventHandler<FtpLogEntry> NewEntry;
    }
}
