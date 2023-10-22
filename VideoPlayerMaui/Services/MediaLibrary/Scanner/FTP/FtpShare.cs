using FluentFTP;
using System;
using System.Linq;
using System.Net;
using VideoPlayer.Services.MediaLibrary.Scanner.Shares;

namespace VideoPlayer.Services.MediaLibrary.Scanner.FTP
{
    public class FtpFileInfo
    {

        public string Name { get; internal set; }

        public string Path { get; internal set; }

    }

    public class FtpShare: RemoteShare
    {

        private string serverName;
        private string username;
        private string password;
        private FtpClient ftpClient;

        public FtpShare(string serverName, string username, string password)
            : base()
        {
            this.serverName = serverName;
            this.username = username;
            this.password = password;
        }

        public void Connect()
        {
            if (ftpClient != null)
                throw new ApplicationException("Already connected!");
            ftpClient = new FtpClient(serverName, new NetworkCredential(username, password), 21);
            ftpClient.Connect();
        }

        public void Disconnect()
        {
            ftpClient.Disconnect();
            ftpClient.Dispose();
            ftpClient = null;
        }

        public bool IsConnected
        {
            get
            {
                return (ftpClient != null) && ftpClient.IsConnected;
            }
        }

        public IEnumerable<FtpFileInfo> ListFiles(string path)
        {
            SetPath(path);
            return ftpClient.GetListing()
                            .Where(f => f.Type == FtpObjectType.File)
                            .Select(f => new FtpFileInfo() { Name = f.Name, Path = f.FullName });
        }

        private void SetPath(string path)
        {
            ftpClient.SetWorkingDirectory("/");
            string[] parts = path.TrimStart('/').Split("/");
            foreach (var part in parts)
                ftpClient.SetWorkingDirectory(part);
        }

        public IEnumerable<FtpFileInfo> ListDirectories(string path)
        {
            SetPath(path);
            return ftpClient.GetListing()
                            .Where(f => f.Type == FtpObjectType.Directory)
                            .Select(f => new FtpFileInfo() { Name = f.Name, Path = f.FullName });
        }

        internal void DownloadFile(string remoteFilePath, string localFilePath)
        {
            if (ftpClient.DownloadFile(localFilePath,
                                       remoteFilePath,
                                       FtpLocalExists.Resume,
                                       FtpVerify.Retry | FtpVerify.Throw) != FtpStatus.Success)
                throw new ApplicationException($"Could not download file {remoteFilePath}.");
        }

        internal void UploadFile(string remoteFilePath, string localFilePath)
        {
            if (ftpClient.UploadFile(localFilePath,
                                     remoteFilePath,
                                     FtpRemoteExists.Skip,
                                     false,
                                     FtpVerify.Retry | FtpVerify.Throw) != FtpStatus.Success)
                throw new ApplicationException($"Could not upload file {remoteFilePath}.");
        }

    }
}
