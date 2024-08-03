using Mediathek.Services.MediaLibrary.Scanner.Events;

namespace Mediathek.Services.MediaLibrary.Scanner.Samba
{
    public class SmbShareFolderEventArgs: FolderEventArgs
    {

        public SmbShareFolderEventArgs(RemoteMediaSource source, SmbShareFolder folder)
            : base(source, folder) { }

    }
}
