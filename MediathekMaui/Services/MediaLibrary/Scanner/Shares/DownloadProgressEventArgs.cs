using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.Scanner.Shares
{
    public class DownloadProgressEventArgs: EventArgs
    {

        public DownloadProgressEventArgs(float progress)
        {
            Progress = progress;
        }

        public DownloadProgressEventArgs(string remoteFilePath, string localFilePath, float progress)
        {
            RemoteFilePath = remoteFilePath;
            LocalFilePath = localFilePath;
            Progress = progress;
        }

        private float progress { get; set; }

        public float Progress
        {
            get
            {
                return progress;
            }
            set
            {
                progress = (float)Math.Round(value * 100, 2);
            }
        }

        public string RemoteFilePath { get; }

        public string LocalFilePath { get; }

        public bool Cancel { get; internal set; }

    }
}
