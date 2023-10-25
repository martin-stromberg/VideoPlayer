using VideoPlayer.Services.MediaLibrary.Scanner.Events;

namespace VideoPlayer.Services.MediaLibrary.Scanner.Samba
{
    public class SmbShareFolderEventArgs: FolderEventArgs
    {

        public SmbShareFolderEventArgs(SmbShareFolder folder)
            : base(folder) { }

    }
}
