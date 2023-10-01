namespace SmbTests.Samba
{
    public class SmbShareFolderEventArgs: EventArgs
    {
        public SmbShareFolderEventArgs(SmbShareFolder folder)
            :base()
        {
            Folder = folder;
        }

        public SmbShareFolder Folder { get; }
    }
}
