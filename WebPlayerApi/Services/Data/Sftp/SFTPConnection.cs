using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.IO;
using WebPlayerApi.Models;

namespace WebPlayerApi.Service.Data.SFtp
{
    public class SFTPConnection : IDisposable
    {
        private static string[] _blackListFileNames = new string[] { ".", "..", "System Volume Information", "$RECYCLE.BIN" };
        private MediaDirectory mediaSource;
        private SftpClient _client;
        public SFTPConnection(MediaDirectory mediaSource)
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

        public bool IsConnected => _client.IsConnected;
        
        internal IEnumerable<ISftpFile> QueryDirectory(string path)
        {
            path = $"{mediaSource.Path}{path}".TrimEnd('/');
            CheckConnection();
            return _client.ListDirectory(path)
                .Where(f => !_blackListFileNames.Contains(f.Name));
        }
        internal Stream ReadFile(string path)
        {
            path = $"{mediaSource.Path}{path}";
            CheckConnection();
            return _client.OpenRead(path);
        }
        internal void Download(string path, string localFilePath, Action<decimal> progressCallback)
        {
            path = $"{mediaSource.Path}{path}";
            CheckConnection();
            using (var stream = File.OpenWrite(localFilePath))
            {
                var fileInfo = _client.Get(path);
                var lastPercent = (decimal)0;
                var lastProgress = DateTime.MinValue;
                _client.DownloadFile(path, stream, (progress) => 
                {
                    var percent = fileInfo.Length == 0 ? -1 : Math.Round(((decimal)progress / (decimal)fileInfo.Length) * 100, 2);
                    if (percent != lastPercent && lastProgress.AddSeconds(1) < DateTime.Now)
                    {
                        lastProgress = DateTime.Now;
                        progressCallback(percent);                        
                    }
                });
            }
        }

        internal void Upload(string localFilePath, string destFilePath, Action<decimal> progressCallback)
        {
            destFilePath = $"{mediaSource.Path}{destFilePath}";
            CheckConnection();
            if (_client.Exists(destFilePath)) 
                throw new ApplicationException($"File {destFilePath} already exists.");
            var fileLen = new FileInfo(localFilePath).Length;
            using (var stream = File.OpenRead(localFilePath))
            {                
                var lastPercent = (decimal)0;
                var lastProgress = DateTime.MinValue;
                _client.UploadFile(stream, destFilePath, (progress) =>
                {
                    var percent = fileLen == 0 ? -1 : Math.Round(((decimal)progress / (decimal)fileLen) * 100, 2);
                    if (percent != lastPercent && lastProgress.AddSeconds(1) < DateTime.Now)
                    {
                        lastProgress = DateTime.Now;
                        progressCallback(percent);
                    }
                });
            }
        }
    }
}
