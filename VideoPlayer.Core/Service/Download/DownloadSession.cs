using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;

namespace VideoPlayer.Service.Download
{
    public class DownloadSession
    {
        public DownloadSession()
        {
            SessionId = Guid.NewGuid();
        }
        public MediaItem Item { get; internal set; }
        public ClassifiedEntry Entry { get; internal set; }
        public decimal DownloadProgress { get; private set; }
        public MediaItemCopyType CopyType { get; internal set; }
        private DownloadStatus _Status = DownloadStatus.Waiting;
        public DownloadStatus Status
        {
            get => _Status;
            private set
            {
                _Status = value;
                switch (value)
                {
                    case DownloadStatus.Downloading:
                        Starting?.Invoke(this, new DownloadEventArgs(Entry)
                        {
                            Session = this
                        });
                        break;
                    case DownloadStatus.Finished:
                        Finished?.Invoke(this, new DownloadEventArgs(Entry)
                        {
                            Session = this
                        });
                        break;
                }
            }
        }

        public TimeSpan DueTime { get; internal set; }
        public Guid SessionId { get; }

        public void Reset()
        {
            Status = DownloadStatus.Waiting;
        }
        public void Start()
        {
            Status = DownloadStatus.Downloading;
        }
        public void Finish()
        {
            Status = DownloadStatus.Finished;
        }
        public void Fail(Exception error)
        {
            Status = DownloadStatus.Failed;
            Failed?.Invoke(this, new DownloadFailedEventArgs(Entry, error)
            {
                Session = this
            });
        }

        public event EventHandler<DownloadEventArgs> Starting;
        public event EventHandler<DownloadEventArgs> Finished;
        public event EventHandler<DownloadFailedEventArgs> Failed;
        public event EventHandler<ProgressEventArgs> Progress;

        private ProgressEventArgs _progressInfo = null;
        internal void SetProgress(decimal progress)
        {
            if (Status != DownloadStatus.Downloading)
                return;
            DownloadProgress = progress;
            _progressInfo = _progressInfo ??= new ProgressEventArgs(progress);
            _progressInfo.Progress = progress;
            Progress?.Invoke(this, _progressInfo);
        }

        public bool Waiting { get; private set; }

        private DownloadSession parentSession;
        private void ClearParent()
        {
            if (parentSession is not null)
            {
                parentSession.Finished -= ParentSession_Finished;
                parentSession.Failed -= ParentSession_Failed;
                parentSession.Progress -= ParentSession_Progress;
                parentSession = null;
            }
            Waiting = false;
            Status = DownloadStatus.Waiting;
        }
        public void Assign(DownloadSession existingSession)
        {
            ClearParent();
            Waiting = true;
            parentSession = existingSession;
            parentSession.Finished += ParentSession_Finished;
            parentSession.Failed += ParentSession_Failed;
            parentSession.Progress += ParentSession_Progress;
        }

        private void ParentSession_Progress(object sender, ProgressEventArgs e)
        {
            Status = DownloadStatus.Downloading;
            SetProgress(e.Progress);
        }

        private void ParentSession_Failed(object sender, DownloadFailedEventArgs e)
        {
            ClearParent();
        }
        private void ParentSession_Finished(object sender, DownloadEventArgs e)
        {
            ClearParent();
        }
    }
}
