using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using static VideoPlayer.Service.Download.DownloadManager;

namespace VideoPlayer.Service.Download
{
    public class DownloadEventArgs : BaseServiceModelEventArgs
    {
        public DownloadEventArgs(BaseServiceModel elementToDownload) 
            : base(elementToDownload)
        {
        }

        public DownloadSession Session { get; set; }
    }
    public class DownloadProgressEventArgs : DownloadEventArgs
    {
        public DownloadProgressEventArgs(
            BaseServiceModel elementToDownload,
            decimal progress) 
            : base(elementToDownload)
        {
            Progress = progress;
        }

        public decimal Progress { get; }
    }
    public class DownloadFailedEventArgs: DownloadEventArgs
    {
        public DownloadFailedEventArgs(
            BaseServiceModel elementToDownload,
            Exception error)
            : base(elementToDownload)
        {
            Error = error;
        }
        public Exception Error { get; }
    }
}
