using Renci.SshNet;
using Renci.SshNet.Sftp;
using VideoPlayer.Service.Library.Models.Sources;

namespace VideoPlayer.Service.Library.SourceReader.SFtp
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
}
