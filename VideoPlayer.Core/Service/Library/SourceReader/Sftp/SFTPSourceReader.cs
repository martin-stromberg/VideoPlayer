using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System.Collections.Concurrent;
using VideoPlayer.Service.ErrorHandling;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Sources;

namespace VideoPlayer.Service.Library.SourceReader.SFtp
{
    [ServiceModelReference(typeof(SFTPMediaSource))]
    public class SFTPSourceReader : SourceReader
    {
        private SFTPConnectionManager _ConnectionManager;

        public SFTPSourceReader(SFTPMediaSource mediaSource) : base(mediaSource)
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

        public SFTPMediaSource FTPMediaSource
        {
            get
            {
                return MediaSource as SFTPMediaSource;
            }
        }

        public override FileInfo Download(MediaItem file, Action<decimal> progressCallback)
        {
            string localFilePath = Path.GetTempFileName();
            File.Delete(localFilePath);
            var localFolderPath = Path.GetDirectoryName(localFilePath);
            if (!Path.Exists(localFolderPath))
                Directory.CreateDirectory(localFolderPath);

            var connection = Connect();
            try
            {
                connection.Download(file.Path, localFilePath, progressCallback);
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
            return new SourceFolder() { FullPath = FTPMediaSource.RootPath, Path = "", Name = FTPMediaSource.Name };
        }

        public override SourceFile ReadFile(MediaItem mediaItem)
        {
            var folderPath = Path.GetDirectoryName(mediaItem.Path);
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
                return (ReadFiles(folder)).FirstOrDefault(f => f.Name == mediaItem.Name);
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
                return File.ReadAllText(tempFile.FullName);
            }
            finally
            {
                tempFile.Refresh();
                if (tempFile.Exists)
                    tempFile.Delete();
            }
        }
    }
}
