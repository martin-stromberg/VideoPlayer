using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.MediaLibrary.Downloads
{
    public enum DownloadStatus { Waiting, Downloading, Finished, Failed }
    public class DownloadSession: BaseModel
    {
        public Database.Models.DownloadJob Job
        {
            get { return GetProperty<Database.Models.DownloadJob>(); }
            private set { SetProperty<Database.Models.DownloadJob>(value); }
        }
        public MediaItem Item {
            get { return GetProperty<MediaItem>(); }
            private set { SetProperty<MediaItem>(value); }
        }
        public DownloadStatus Status {
            get { return GetProperty<DownloadStatus>(); }
            private set { SetProperty<DownloadStatus>(value); }
        }
        public float Progress
        {
            get { return GetProperty<float>(); }
            private set { SetProperty<float>(value); }
        }

        public string ErrorMessage {
            get { return GetProperty<string>(); }
            private set { SetProperty<string>(value); }
        }

        public int ErrorCounter {
            get { return GetProperty<int>(); }
            private set { SetProperty<int>(value); }
        }

        private DownloadSession()
        {
        }

        internal static DownloadSession CreateFinished(MediaItem item)
        {
            return new DownloadSession()
            {
                Job = null,
                Item = item,
                Status = DownloadStatus.Finished,
                Progress = 100
            };
        }

        internal static DownloadSession CreateFromJob(Database.Models.DownloadJob job)
        {
            return new DownloadSession()
            {
                Job = job,
                Item = null,
                Status = DownloadStatus.Waiting,
                Progress = 0
            };
        }

        internal void SetCanceled()
        {
            Status = DownloadStatus.Waiting;
        }

        internal void SetStarted()
        {
            Status = DownloadStatus.Downloading;
        }

        internal void SetFinished(MediaItem item)
        {
            Item = item;
            Status = DownloadStatus.Finished;
        }

        internal void SetFailed(Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = DownloadStatus.Failed;
            ErrorCounter += 1;
        }
        internal void SetProgress(float progress)
        {
            Progress = progress;
        }
    }
}
