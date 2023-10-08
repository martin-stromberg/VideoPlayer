namespace VideoPlayerLib.Services.Common
{
    public class FileEventArgs : EventArgs
    {
        public FileEventArgs(RemoteFile file)
            : base()
        {
            File = file;
        }

        public RemoteFile File { get; }
    }
}
