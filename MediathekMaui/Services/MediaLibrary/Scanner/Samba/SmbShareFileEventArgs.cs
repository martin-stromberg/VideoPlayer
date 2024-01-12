using Mediathek.Services.MediaLibrary.Scanner.Events;

namespace Mediathek.Services.MediaLibrary.Scanner.Samba
{
    public class SmbShareFileEventArgs: FileEventArgs
    {

        public SmbShareFileEventArgs(SmbShareFile file)
            : base(file)
        {
            File = file;
        }

        public SmbShareFile File { get; }

    }
}
