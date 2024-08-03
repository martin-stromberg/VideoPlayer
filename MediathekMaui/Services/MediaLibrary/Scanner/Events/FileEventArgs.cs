using Mediathek.Services.MediaLibrary.Scanner.Models;

namespace Mediathek.Services.MediaLibrary.Scanner.Events
{
    public class FileEventArgs: EventArgs
    {

        public FileEventArgs(RemoteMediaSource source, RemoteFile file)
            : base()
        {
            Source = source;
            File = file;
        }

        public RemoteMediaSource Source { get; }
        public RemoteFile File { get; }

    }
}
