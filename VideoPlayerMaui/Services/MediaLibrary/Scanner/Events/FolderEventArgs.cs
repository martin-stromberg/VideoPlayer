using VideoPlayer.Services.MediaLibrary.Scanner.Models;

namespace VideoPlayer.Services.MediaLibrary.Scanner.Events
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
