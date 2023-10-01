using VideoPlayerLib.Services.Common;

namespace VideoPlayerLib.Services.Samba
{
    public class SmbShareFileEventArgs : FileEventArgs
    {
        public SmbShareFileEventArgs(SmbShareFile file)
            : base(file)
        {
            File = file;
        }

        public SmbShareFile File { get; }
    }
}
