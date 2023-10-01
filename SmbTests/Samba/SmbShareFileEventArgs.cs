namespace SmbTests.Samba
{
    public class SmbShareFileEventArgs : EventArgs
    {
        public SmbShareFileEventArgs(SmbShareFile file)
            : base()
        {
            File = file;
        }

        public SmbShareFile File { get; }
    }
}
