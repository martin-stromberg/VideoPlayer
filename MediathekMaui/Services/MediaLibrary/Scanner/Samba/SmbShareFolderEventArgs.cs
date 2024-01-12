using Mediathek.Services.MediaLibrary.Scanner.Events;

namespace Mediathek.Services.MediaLibrary.Scanner.Samba
{
    public class SmbShareFolderEventArgs: FolderEventArgs
    {

        public SmbShareFolderEventArgs(SmbShareFolder folder)
            : base(folder) { }

    }
}
