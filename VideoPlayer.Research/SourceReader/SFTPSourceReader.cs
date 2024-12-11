using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.SourceReader;

namespace VideoPlayer.Research.SourceReader
{
    public class SFTPConnection : IDisposable
    {
        private static string[] _blackListFileNames = new string[] { ".", "..", "System Volume Information", "$RECYCLE.BIN" };
        private SFTPMediaSource mediaSource;
        private SftpClient _client;
        public SFTPConnection(SFTPMediaSource mediaSource)
        {
            this.mediaSource = mediaSource;
            _client = new SftpClient(mediaSource.Servername, mediaSource.Port, mediaSource.Username, mediaSource.Password);
        }
        ~SFTPConnection()
        {
            Dispose();
        }
        public long Id { get; } = DateTime.Now.Ticks;

        public void Dispose()
        {
            
            if (_client is not null)
            {
                if (_client.IsConnected)
                    _client.Disconnect();
                _client = null;
            }
        }

        internal void CheckConnection()
        {
            if (_client.IsConnected) return;
            _client.Connect();            
        }
        
        internal IEnumerable<ISftpFile> QueryDirectory(string path)
        {
            path = $"{mediaSource.RootPath}{path}".TrimEnd('/');
            CheckConnection();
            return _client.ListDirectory(path)
                .Where(f => !_blackListFileNames.Contains(f.Name));
        }

        internal void Download(string path, string localFilePath, Action<decimal> progressCallback)
        {
            path = $"{mediaSource.RootPath}{path}";
            CheckConnection();
            using (var stream = File.OpenWrite(localFilePath))
            {
                var fileInfo = _client.Get(path);                
                _client.DownloadFile(path, stream, (progress) => 
                {
                    var percent = fileInfo.Length == 0 ? -1 : ((long)progress / fileInfo.Length) * 100;
                    progressCallback(percent);
                });
            }
        }
    }
    public class SFTPConnectionManager
    {
        private object _collectionLock = new object();
        private ConcurrentQueue<SFTPConnection> _free = new ConcurrentQueue<SFTPConnection>();
        private ConcurrentDictionary<long, SFTPConnection> _inUse = new ConcurrentDictionary<long, SFTPConnection>();
        public SFTPConnectionManager(SFTPMediaSource mediaSource)
        {
            MediaSource = mediaSource;
        }
        public int ConcurrentConnections { get; set; } = 1;
        public SFTPMediaSource MediaSource { get; private set; }

        public async Task<SFTPConnection> Connect()
        {
            SFTPConnection connection = null;
            while (connection is null)
            {
                await Task.Delay(100);
                lock (_collectionLock)
                    if (!_free.TryDequeue(out connection))
                        connection = CreateNewConnection();
            }
            _inUse.AddOrUpdate(connection.Id, connection, (key, existing) => existing);
            return connection;
        }
        public void Release(SFTPConnection connection)
        {
            lock (_collectionLock)
            {
                if (!_inUse.Remove(connection.Id, out var inUseConnection))
                    connection.Dispose();
                else if (_free.Count() < ConcurrentConnections)
                    _free.Enqueue(inUseConnection);
                else
                    inUseConnection.Dispose();
            }
        }

        private SFTPConnection CreateNewConnection()
        {
            if (_inUse.Count() >= ConcurrentConnections)
                return null;
            return new SFTPConnection(MediaSource)
            {

            };
        }

        public void Clear()
        {
            MediaSource = null;
            ConcurrentConnections = 0;
        }
    }
    public class SFTPSourceReader : BaseSourceReader
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

        protected async Task<SFTPConnection> ConnectAsync()
        {
            var connection = await _ConnectionManager.Connect();
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

            var connection = ConnectAsync().Result;
            try
            {
                connection.Download(file.Path, localFilePath, progressCallback);
            }
            finally
            {
                Release(connection);
            }
            return new FileInfo(localFilePath);
        }

        public override SourceFolder GetRoot()
        {
            return new SourceFolder() { FullPath = FTPMediaSource.RootPath, Path = "", Name = FTPMediaSource.Name };
        }

        public override async Task<SourceFile> ReadFileAsync(MediaItem mediaItem)
        {
            var folderPath = Path.GetDirectoryName(mediaItem.Path);
            var folder = GetRoot();
            SourceFolder[] subFolders;
            while (folder is not null && folder.Path != folderPath)
            {
                subFolders = (await ReadFoldersAsync(folder)).ToArray();
                folder = subFolders.FirstOrDefault(f =>
                    folderPath.StartsWith(f.Path)
                    && f.Path.Length <= folderPath.Length
                    && folderPath.Substring(0, f.Path.Length) == f.Path
                    && (folderPath.Remove(0, f.Path.Length) == ""
                    || folderPath.Remove(0, f.Path.Length).StartsWith("/"))
                    );
            }
            if (folder is not null)
                return (await ReadFilesAsync(folder)).FirstOrDefault(f => f.Name == mediaItem.Name);
            return null;
        }

        public override async Task<IEnumerable<SourceFile>> ReadFilesAsync(SourceFolder folder)
        {
            var connection = await ConnectAsync();
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

        public override async Task<IEnumerable<SourceFolder>> ReadFoldersAsync(SourceFolder folder)
        {
            var connection = await ConnectAsync();
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
