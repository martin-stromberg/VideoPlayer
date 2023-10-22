using VideoPlayer.Services.MediaLibrary.Scanner.Events;

namespace VideoPlayer.Services.MediaLibrary.Scanner.Samba
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
