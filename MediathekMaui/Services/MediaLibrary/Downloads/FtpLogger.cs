using FluentFTP;
using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.Downloads
{
    internal class FtpLogger: IFtpLogger
    {

        public void Log(FtpLogEntry entry)
        {
            NewEntry?.Invoke(this, entry);
        }

        public event EventHandler<FtpLogEntry> NewEntry;

    }
}
