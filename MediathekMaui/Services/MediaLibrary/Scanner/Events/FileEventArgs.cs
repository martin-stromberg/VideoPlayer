using Mediathek.Services.MediaLibrary.Scanner.Models;

namespace Mediathek.Services.MediaLibrary.Scanner.Events
{
    public class FileEventArgs: EventArgs
    {

        public FileEventArgs(RemoteFile file)
            : base()
        {
            File = file;
        }

        public RemoteFile File { get; }

    }
}
