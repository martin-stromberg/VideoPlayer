using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Diagnostics;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Extensions;
using VideoPlayer.Service.Library.SourceReader;
using SMBLibrary.Client;
using SMBLibrary;
using System.Net;
using SMBLibrary.SMB1;

namespace VideoPlayer.Research.SourceReader
{
    public class SmbConnection: IDisposable
    {
        private static string[] _blackListFileNames = new string[] { ".", "..", "System Volume Information", "$RECYCLE.BIN" };
        private SmbMediaSource mediaSource;
        private SMB2Client _client = new SMB2Client();
        private bool _logedIn = false;
        private bool _TreeConnected = false;
        private ISMBFileStore _FileStore = null;
        public SmbConnection(SmbMediaSource mediaSource)
        {
            this.mediaSource = mediaSource;
        }
        ~SmbConnection()
        {
            Dispose();
        }
        public long Id { get; } = DateTime.Now.Ticks;

        public void Dispose()
        {
            if (_FileStore is not null)
            {
                _ = _FileStore.Disconnect();
                _FileStore = null;
                _TreeConnected = false;
            }
            if (_client is not null)
            {
                Logoff();
                if (_client.IsConnected)
                    _client.Disconnect();
                _client = null;
            }
        }

        private void Logoff()
        {
            if (_logedIn)
            {
                _client.Logoff();
                _logedIn = false;
            }
        }

        internal void CheckConnection()
        {
            if (_client.IsConnected) return;
            _logedIn = false;
            if (_client.Connect(IPAddress.Parse(mediaSource.Servername), SMBTransportType.DirectTCPTransport)) return;
            throw new InvalidOperationException($"Connection to {mediaSource.Servername} could not be established.");
        }
        internal void CheckLogin()
        {
            CheckConnection();
            if (_logedIn) return;
            _TreeConnected = false;
            NTStatus status = _client.Login(String.Empty, mediaSource.Username, mediaSource.Password);
            _logedIn = status == NTStatus.STATUS_SUCCESS;
            if (_logedIn) return;
            throw new InvalidOperationException($"Login to {mediaSource.Servername} with user {mediaSource.Username} could not be established.");
        }
        internal void CheckTreeConnect()
        {
            CheckLogin();
            if (_FileStore is not null) return;
            _FileStore = _client.TreeConnect(mediaSource.ShareName, out var status);
            _TreeConnected = status == NTStatus.STATUS_SUCCESS;
            if (_TreeConnected) return;
            throw new InvalidOperationException($"Could not connect to file tree.");
        }

        internal IEnumerable<FileDirectoryInformation> QueryDirectory(string path)
        {
            path = $"{mediaSource.RootPath}\\{path.Replace("/", "\\").TrimStart('\\')}".TrimEnd('\\');
            CheckTreeConnect();
            var status = _FileStore.CreateFile(out var directoryHandle, out var fileStatus, path, AccessMask.GENERIC_READ, SMBLibrary.FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            if (status != NTStatus.STATUS_SUCCESS)
                throw new InvalidOperationException($"Could not create directory file handle.");
            try
            {
                status = _FileStore
                    .QueryDirectory(out var fileList, directoryHandle, "*", FileInformationClass.FileDirectoryInformation);
                return fileList.Cast<FileDirectoryInformation>()
                    .Where(f => !_blackListFileNames.Contains(f.FileName));
            }
            finally
            {
                status = _FileStore.CloseFile(directoryHandle);
            }
        }
    }
    public class SmbConnectionManager
    {
        private object _collectionLock = new object();
        private ConcurrentQueue<SmbConnection> _free = new ConcurrentQueue<SmbConnection>();
        private ConcurrentDictionary<long, SmbConnection> _inUse = new ConcurrentDictionary<long, SmbConnection>();
        public SmbConnectionManager(SmbMediaSource mediaSource)
        {
            MediaSource = mediaSource;
        }
        public int ConcurrentConnections { get; set; } = 1;
        public SmbMediaSource MediaSource { get; private set; }

        public async Task<SmbConnection> Connect()
        {
            SmbConnection connection = null;
            while (connection is null)
            {
                await Task.Delay(100);
                lock (_collectionLock)
                    if (!_free.TryDequeue(out connection))
                        connection = CreateNewConnection();
            }
            _inUse.AddOrUpdate(connection.Id, connection,(key, existing) => existing);
            return connection;
        }
        public void Release(SmbConnection connection)
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

        private SmbConnection CreateNewConnection()
        {
            if (_inUse.Count() >= ConcurrentConnections)
                return null;
            return new SmbConnection(MediaSource)
            {

            };
        }

        public void Clear()
        {
            MediaSource = null;
            ConcurrentConnections = 0;
        }
    }
    public class SmbSourceReader : BaseSourceReader
    {
        private SmbConnectionManager _ConnectionManager;

        public SmbSourceReader(SmbMediaSource mediaSource) : base(mediaSource)
        {
            _ConnectionManager = new SmbConnectionManager(mediaSource);
        }
        ~SmbSourceReader()
        {
            _ConnectionManager.Clear();
        }

        public SmbMediaSource SambaMediaSource
        {
            get
            {
                return MediaSource as SmbMediaSource;
            }
        }

        protected async Task<SmbConnection> ConnectAsync()
        {
            var connection = await _ConnectionManager.Connect();
            try
            {
                connection.CheckLogin();
            }
            catch
            {
                _ConnectionManager.Release(connection);
                connection = null;
                throw;
            }
            return connection;
        }
        private void Release(SmbConnection connection)
        {
            _ConnectionManager.Release(connection);
        }

        public override FileInfo Download(MediaItem nfoFile, Action<decimal> progressCallback)
        {
            //SMBLibrary

            throw new NotImplementedException();
        }

        public override SourceFolder GetRoot()
        {
            return new SourceFolder() { FullPath = SambaMediaSource.Path, Path = "", Name = SambaMediaSource.Name };
        }

        public override Task<SourceFile> ReadFileAsync(MediaItem mediaItem)
        {
            throw new NotImplementedException();
        }

        public override async Task<IEnumerable<SourceFile>> ReadFilesAsync(SourceFolder folder)
        {
            var connection = await ConnectAsync();
            try
            {
                var fileList = connection.QueryDirectory($"{folder.Path}");
                return fileList
                    .Where(file => file.FileAttributes != SMBLibrary.FileAttributes.Directory)
                    .Select(file =>                
                {
                    return new SourceFile()
                    {
                        Path = $"{folder.Path}\\{file.FileName}",
                        FullPath = $"{folder.FullPath}\\{file.FileName}",
                        Name = file.FileName,
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
                    .Where(file => file.FileAttributes == SMBLibrary.FileAttributes.Directory)
                    .Select(file =>
                    {                        
                        return new SourceFolder()
                        {
                            Path = $"{folder.Path}\\{file.FileName}",
                            FullPath = $"{folder.FullPath}\\{file.FileName}",
                            Name = file.FileName,
                            LastWriteTime = file.LastWriteTime
                        };
                    });
            }
            finally
            {
                Release(connection);
            }
        }

        public override string ReadTextFile(MediaItem nfoFile)
        {
            throw new NotImplementedException();
        }
    }
}
