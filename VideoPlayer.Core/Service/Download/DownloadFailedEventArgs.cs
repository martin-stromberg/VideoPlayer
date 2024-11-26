using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Download
{
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
