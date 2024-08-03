using Mediathek.Services.MediaLibrary.Scanner.Events;

namespace Mediathek.Services.MediaLibrary.Scanner.Samba
{
    public class SmbShareFileEventArgs: FileEventArgs
    {

        public SmbShareFileEventArgs(RemoteMediaSource source, SmbShareFile file)
            : base(source, file)
        {
            File = file;
        }

        public SmbShareFile File { get; }

    }
}
