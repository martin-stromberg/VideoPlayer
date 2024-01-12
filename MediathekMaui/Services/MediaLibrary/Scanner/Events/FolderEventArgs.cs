using Mediathek.Services.MediaLibrary.Scanner.Models;

namespace Mediathek.Services.MediaLibrary.Scanner.Events
{
    public class FolderEventArgs: EventArgs
    {

        public FolderEventArgs(RemoteFolder folder)
            : base()
        {
            Folder = folder;
        }

        public RemoteFolder Folder { get; }

    }
}
