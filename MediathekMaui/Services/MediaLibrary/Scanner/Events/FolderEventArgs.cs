using Mediathek.Services.MediaLibrary.Scanner.Models;

namespace Mediathek.Services.MediaLibrary.Scanner.Events
{
    public class FolderEventArgs: EventArgs
    {

        public FolderEventArgs(
            RemoteMediaSource source,
            RemoteFolder folder)
            : base()
        {
            Source = source;
            Folder = folder;
        }

        public RemoteMediaSource Source { get; }
        public RemoteFolder Folder { get; }

    }
}
