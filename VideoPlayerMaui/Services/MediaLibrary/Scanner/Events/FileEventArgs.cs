using VideoPlayer.Services.MediaLibrary.Scanner.Models;

namespace VideoPlayer.Services.MediaLibrary.Scanner.Events
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
