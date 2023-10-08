using System;
using System.Linq;

namespace VideoPlayerLib.Services.Common
{
    public abstract class RemoteSourceScanner
    {
        public event EventHandler<FolderEventArgs> FolderFound;
        protected void OnFolderFound(FolderEventArgs e)
        {
            FolderFound?.Invoke(this, e);
        }
        protected virtual Folder OnFolderFound(Folder folder)
        {
            OnFolderFound(new FolderEventArgs(folder));
            return folder;
        }
        public event EventHandler<FileEventArgs> FileFound;
        protected void OnMediaItemFound(FileEventArgs e)
        {
            FileFound?.Invoke(this, e);
        }
        protected virtual Common.RemoteFile OnMediaItemFound(Common.RemoteFile file)
        {
            OnMediaItemFound(new FileEventArgs(file));
            return file;
        }

        public abstract void Scan(string path);
        public abstract IEnumerable<RemoteFile> FindFiles(string path, string fileMask = "*.*");
        public abstract string ReadTextFile(string filePath);
        public abstract void DownloadFile(string sourceFilePath, string destFilePath);
        public abstract void WriteTextFile(string nfoPath, string innerXml);
    }
}
