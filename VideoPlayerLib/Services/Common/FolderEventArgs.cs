namespace VideoPlayerLib.Services.Common
{
    public class FolderEventArgs : EventArgs
    {
        public FolderEventArgs(Folder folder)
            : base()
        {
            Folder = folder;
        }

        public Folder Folder { get; }
    }
}
