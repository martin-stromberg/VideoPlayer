using VideoPlayerLib.Services.Common;

namespace VideoPlayerLib.Services.Samba
{
    public class SmbShareFolderEventArgs: FolderEventArgs
    {
        public SmbShareFolderEventArgs(SmbShareFolder folder)
            :base(folder)
        {
        }
    }
}
