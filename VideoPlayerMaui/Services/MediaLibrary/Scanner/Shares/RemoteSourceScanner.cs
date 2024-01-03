using System;
using System.Linq;
using VideoPlayer.Common;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Sources;
using VideoPlayer.Services.MediaLibrary.Scanner.Events;
using VideoPlayer.Services.MediaLibrary.Scanner.Models;

namespace VideoPlayer.Services.MediaLibrary.Scanner.Shares
{

    public abstract class RemoteSourceScanner
    {

        public event EventHandler ScanCompleted;

        public RemoteMediaSource CurrentSource { get; protected set; }

        protected virtual void OnScanCompleted()
        {
            ScanCompleted?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<FolderScanEventArgs> BeforeScanFolder;

        protected virtual void OnBeforeScanFolder(FolderScanEventArgs e)
        {
            BeforeScanFolder?.Invoke(this, e);
        }
        public event EventHandler<FolderScanEventArgs> AfterScanFolder;
        protected virtual void OnAfterScanFolder(FolderScanEventArgs e)
        {
            AfterScanFolder?.Invoke(this, e);
        }

        public event EventHandler<FolderEventArgs> FolderFound;

        protected void OnFolderFound(FolderEventArgs e)
        {
            FolderFound?.Invoke(this, e);
        }

        protected virtual RemoteFolder OnFolderFound(RemoteFolder folder)
        {
            OnFolderFound(new FolderEventArgs(folder));
            return folder;
        }

        public event EventHandler<FileEventArgs> FileFound;

        protected void OnMediaItemFound(FileEventArgs e)
        {
            FileFound?.Invoke(this, e);
        }

        protected virtual RemoteFile OnMediaItemFound(RemoteFile file)
        {
            OnMediaItemFound(new FileEventArgs(file));
            return file;
        }

        public event EventHandler<ExceptionEventArgs> Error;

        protected virtual void OnError(ExceptionEventArgs e)
        {
            Error?.Invoke(this, e);
        }

        protected virtual void OnError(Exception error)
        {
            Error?.Invoke(this, new ExceptionEventArgs(error));
        }

        public abstract bool CanScan(MediaSource source);

        public abstract void Scan(MediaSource source, bool noContinue);

        public abstract void Scan(MediaSource source, MediaItem mediaItem);

        public abstract IEnumerable<RemoteFile> FindFiles(MediaSource source, string path, string fileMask = "*.*");

        public abstract IEnumerable<RemoteFile> FindFiles(string path, string fileMask = "*.*");

        public abstract IEnumerable<RemoteFolder> FindFolders(
            RemoteMediaSource source,
            string path,
            string folderNameMask);

        public abstract string ReadTextFile(string filePath);

        public abstract void DownloadFile(string sourceFilePath, string destFilePath);

        public abstract void WriteTextFile(string nfoPath, string innerXml);

        public abstract bool TestConnection(MediaSource mediaSource);

        internal abstract void SavePictureFromUri(string imageURL, string imageFolderPath);
    }
}
