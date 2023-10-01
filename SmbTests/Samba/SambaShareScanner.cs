using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SmbTests.Samba
{
    public class SambaShareScanner
    {
        private readonly SambaShare share;
        private static string[] FolderNameBlacklist = { "$RECYCLE.BIN", "System Volume Information", "lost+found" };

        public SambaShareScanner(SambaShare share)
        {
            this.share = share;
        }

        public void Scan(string path)
        {
            Scan(path, false);
        }
        private void Scan(string path, bool isSubFolder)
        {
            if (!isSubFolder)
                share.ConnectAndLogin();
            try
            {
                var files = share.ListFiles(path)
                    .ToArray();
                foreach (var file in files)
                {
                    var shareFile = new SmbShareFile()
                    {
                        Name = file.FileName,
                        Path = Path.Combine(path, file.FileName)
                    };
                    OnMediaItemFound(shareFile);
                }
                var folders = share.ListDirectories(path)
                    .Where(f => !FolderNameBlacklist.Contains(f.FileName))
                    .ToArray();
                foreach (var folder in folders)
                {
                    var shareFolder = new SmbShareFolder()
                    {
                        Name = folder.FileName,
                        Path = Path.Combine(path, folder.FileName)
                    };
                    OnFolderFound(shareFolder);
                    Scan(shareFolder.Path, true);
                }                    
            }
            finally
            {
                if (!isSubFolder)
                    share.LogoffAndDisconnect();
            }
        }

        public event EventHandler<SmbShareFolderEventArgs> FolderFound;
        private SmbShareFolder OnFolderFound(SmbShareFolder folder)
        {
            FolderFound?.Invoke(this, new SmbShareFolderEventArgs(folder));
            return folder;
        }
        public event EventHandler<SmbShareFileEventArgs> FileFound;
        private SmbShareFile OnMediaItemFound(SmbShareFile file)
        {
            FileFound?.Invoke(this, new SmbShareFileEventArgs(file));
            return file;
        }
    }
}
