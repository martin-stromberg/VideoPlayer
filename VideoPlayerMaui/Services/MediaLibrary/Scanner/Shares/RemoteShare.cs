using System;
using System.Linq;

namespace VideoPlayer.Services.MediaLibrary.Scanner.Shares
{
    public abstract class RemoteShare
    {
        public event EventHandler<DownloadProgressEventArgs> DownloadProgress;
        protected void OnDownloadProgress(DownloadProgressEventArgs e)
        {
            DownloadProgress?.Invoke(this, e);
        }

        public abstract void DownloadFile(string remoteFilePath, string localFilePath);
    }
}
