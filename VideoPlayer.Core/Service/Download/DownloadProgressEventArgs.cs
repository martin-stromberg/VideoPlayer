using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Download
{
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
}
