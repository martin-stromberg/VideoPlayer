using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System.Collections.Concurrent;
using WebPlayerApi.Models;
using static System.Net.WebRequestMethods;

namespace WebPlayerApi.Service.Data.SFtp
{
    public class SFTPSourceReader : SourceReader
    {
        private SFTPConnectionManager _ConnectionManager;

        public SFTPSourceReader(MediaDirectory mediaSource) : base(mediaSource)
        {
            _ConnectionManager = new SFTPConnectionManager(mediaSource);
        }
        ~SFTPSourceReader()
        {
            _ConnectionManager.Clear();
        }

        protected SFTPConnection Connect()
        {
            var connection = _ConnectionManager.Connect();
            try
            {
                connection.CheckConnection();
            }
            catch
            {
                _ConnectionManager.Release(connection);
                connection = null;
                throw;
            }
            return connection;
        }
        private void Release(SFTPConnection connection)
        {
            _ConnectionManager.Release(connection);
        }

        public MediaDirectory FTPMediaSource
        {
            get
            {
                return MediaSource as MediaDirectory;
            }
        }
        public Stream ReadStream(MediaItem file)
        {
            var connection = Connect();
            try
            {
                var stream = connection.ReadFile(file.FilePath);
                // Wir wrappen den Stream in einen DisposableStream, der beim Schließen auch die SFTP-Verbindung trennt
                var wrappedStream = new SftpWrappedStream(stream, connection);
                wrappedStream.ConnectionClosed += WrappedStream_ConnectionClosed;
                return wrappedStream;
            }
            catch (SftpPathNotFoundException ex)
            {
                Release(connection);
                throw new FileDeletedException(ex.Message, ex);
            }
            finally
            {
                
            }
        }

        private void WrappedStream_ConnectionClosed(object? sender, SFTPConnection e)
        {
            ((SftpWrappedStream)sender).ConnectionClosed -= WrappedStream_ConnectionClosed;
            Release(e);
        }

        public override FileInfo Download(MediaItem file, Action<decimal> progressCallback)
        {
            string localFilePath = Path.GetTempFileName();
            System.IO.File.Delete(localFilePath);
            var localFolderPath = Path.GetDirectoryName(localFilePath);
            if (!Path.Exists(localFolderPath))
                Directory.CreateDirectory(localFolderPath);

            var connection = Connect();
            try
            {
                connection.Download(file.FilePath, localFilePath, progressCallback);
            }
            catch(SftpPathNotFoundException ex)
            {
                throw new FileDeletedException(ex.Message, ex);
            }
            finally
            {
                Release(connection);
            }
            return new FileInfo(localFilePath);
        }
        public override void Upload(string sourceFilePath, string destFilePath, Action<decimal> progressCallback)
        {
            var connection = Connect();
            try
            {
                connection.Upload(sourceFilePath, destFilePath, progressCallback);
            }
            finally
            {
                Release(connection);
            }
        }
        public override SourceFolder GetRoot()
        {
            return new SourceFolder() { FullPath = FTPMediaSource.Path, Path = "", Name = FTPMediaSource.Name };
        }

        public override SourceFile ReadFile(MediaItem mediaItem)
        {
            var folderPath = Path.GetDirectoryName(mediaItem.FilePath);
            var folder = GetRoot();
            SourceFolder[] subFolders;
            while (folder is not null && folder.Path != folderPath)
            {
                subFolders = (ReadFolders(folder)).ToArray();
                folder = subFolders.FirstOrDefault(f =>
                    folderPath.StartsWith(f.Path)
                    && f.Path.Length <= folderPath.Length
                    && folderPath.Substring(0, f.Path.Length) == f.Path
                    && (folderPath.Remove(0, f.Path.Length) == ""
                    || folderPath.Remove(0, f.Path.Length).StartsWith("/"))
                    );
            }
            if (folder is not null)
                return (ReadFiles(folder)).FirstOrDefault(f => f.Name == Path.GetFileName(mediaItem.FilePath));
            return null;
        }

        public override IEnumerable<SourceFile> ReadFiles(SourceFolder folder)
        {
            var connection = Connect();
            try
            {
                var fileList = connection.QueryDirectory($"{folder.Path}");
                return fileList
                    .Where(file => !file.IsDirectory)
                    .Select(file =>
                    {
                        return new SourceFile()
                        {
                            Path = $"{folder.Path}/{file.Name}",
                            FullPath = $"{folder.FullPath}/{file.Name}",
                            Name = file.Name,
                            LastWriteTime = file.LastWriteTime
                        };
                    });
            }
            finally
            {
                Release(connection);
            }
        }

        public override IEnumerable<SourceFolder> ReadFolders(SourceFolder folder)
        {
            var connection = Connect();
            try
            {
                var fileList = connection.QueryDirectory($"{folder.Path}");
                return fileList
                    .Where(file => file.IsDirectory)
                    .Select(file =>
                    {
                        return new SourceFolder()
                        {
                            Path = $"{folder.Path}/{file.Name}",
                            FullPath = $"{folder.FullPath}/{file.Name}",
                            Name = file.Name,
                            LastWriteTime = file.LastWriteTime
                        };
                    });
            }
            finally
            {
                Release(connection);
            }
        }

        public override string ReadTextFile(MediaItem file)
        {
            var tempFile = Download(file, (p) => { });
            try
            {
                return System.IO.File.ReadAllText(tempFile.FullName);
            }
            finally
            {
                tempFile.Refresh();
                if (tempFile.Exists)
                    tempFile.Delete();
            }
        }
        public class ScanResultEventArgs: EventArgs
        {
            public SourceFile File { get; set; }
            public bool Continue { get; set; } = true;
            public bool SkipCurrentFolder { get; set; } = false;
        }
        private class ProcessAbortedException: ApplicationException;
        private class FolderAbortedException : ApplicationException;
        internal void CollectMediaItems(string path, Action<ScanResultEventArgs> callback)
        {
            try
            {
                var folder = new SourceFolder()
                {
                    Path = path,
                };
                CollectMediaItems(folder, callback);
            }
            catch (ProcessAbortedException) { }
            catch (FolderAbortedException) { }
        }
        internal void CollectMediaItems(Action<ScanResultEventArgs> callback)
        {
            try
            {
                var root = GetRoot();
                CollectMediaItems(root, callback);
            }
            catch (ProcessAbortedException) { }
            catch (FolderAbortedException) { }
        }

        private void CollectMediaItems(SourceFolder root, Action<ScanResultEventArgs> callback)
        {
            var result = new ScanResultEventArgs();
            foreach (var file in ReadFiles(root))
            {
                result.File = file;
                callback(result);
                if (!result.Continue)
                    throw new ProcessAbortedException();
                if (result.SkipCurrentFolder)
                    throw new FolderAbortedException();
            }
            foreach (var folder in ReadFolders(root))
                try
                {
                    CollectMediaItems(folder, callback);
                }
                catch (FolderAbortedException) { }
        }
    }
}
