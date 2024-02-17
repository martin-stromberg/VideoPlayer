using FluentFTP;
using FluentFTP.Client.BaseClient;
using Mediathek.Services.MediaLibrary.Scanner.Shares;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Linq;
using System.Net;

namespace Mediathek.Services.MediaLibrary.Scanner.FTP
{

    public class FtpShare: RemoteShare
    {

        private string serverName;
        private string username;
        private string password;
        private FtpClient ftpClient;
        private SftpClient sftpClient;
        private byte port = 21;
        private bool isSecureMode = false;

        public FtpShare(string serverName, string username, string password)
            : base()
        {
            this.serverName = serverName;
            this.username = username;
            this.password = password;
        }

        public void Connect()
        {
            if (ftpClient is not null)
                throw new ApplicationException("Already connected!");
            if (sftpClient is not null)
                throw new ApplicationException("Already connected!");
            isSecureMode = false;
            ftpClient = new FtpClient(serverName, new NetworkCredential(username, password), port);
            ftpClient.ValidateCertificate += FtpClient_ValidateCertificate;
            try
            {
                ftpClient.Connect();
                return;
            }
            catch
            {
                Disconnect();
            }

            sftpClient = new SftpClient(serverName, username, password);
            sftpClient.HostKeyReceived += SftpClient_HostKeyReceived;
            try
            {
                sftpClient.Connect();
                isSecureMode = true;
                return;
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        private void SftpClient_HostKeyReceived(object sender, HostKeyEventArgs e)
        {
            e.CanTrust = true;
        }

        private void FtpClient_ValidateCertificate(BaseFtpClient control, FtpSslValidationEventArgs e)
        {
            e.Accept = true;
        }

        public void Disconnect()
        {
            if (ftpClient is not null)
            {
                if (ftpClient.IsConnected)
                    ftpClient.Disconnect();
                ftpClient.ValidateCertificate -= FtpClient_ValidateCertificate;
                ftpClient.Dispose();
                ftpClient = null;
            }
            if (sftpClient is not null)
            {
                if (sftpClient.IsConnected)
                    sftpClient.Disconnect();
                sftpClient.HostKeyReceived -= SftpClient_HostKeyReceived;
                sftpClient.Dispose();
                sftpClient = null;
            }
            isSecureMode = false;
        }

        public bool IsConnected
        {
            get
            {
                return ((ftpClient is not null) && ftpClient.IsConnected)
                    || ((sftpClient is not null) && sftpClient.IsConnected);
            }
        }

        public IEnumerable<FtpFileInfo> ListFiles(string path)
        {
            SetPath(path);
            switch (isSecureMode)
            {
                case true:
                    return sftpClient.ListDirectory(path)
                                     .Where(f => f.IsRegularFile)
                                     .Select(f => new FtpFileInfo() { Name = f.Name, Path = f.FullName });
                case false:

                    return ftpClient.GetListing()
                                    .Where(f => f.Type == FtpObjectType.File)
                                    .Select(f => new FtpFileInfo() { Name = f.Name, Path = f.FullName });
            }
        }

        private void SetPath(string path)
        {
            switch (isSecureMode)
            {
                case true:
                    break;
                case false:
                    ftpClient.SetWorkingDirectory("/");
                    string[] parts = path.TrimStart('/').Split("/");
                    foreach (var part in parts)
                        ftpClient.SetWorkingDirectory(part);
                    break;
            }
        }

        public IEnumerable<FtpFileInfo> ListDirectories(string path)
        {
            SetPath(path);
            switch (isSecureMode)
            {
                case true:
                    return sftpClient.ListDirectory(path)
                                     .Where(f => f.IsDirectory)
                                     .Select(f => new FtpFileInfo() { Name = f.Name, Path = f.FullName });
                case false:
                    return ftpClient.GetListing()
                                    .Where(f => f.Type == FtpObjectType.Directory)
                                    .Select(f => new FtpFileInfo() { Name = f.Name, Path = f.FullName });
            }
        }

        public override void DownloadFile(string remoteFilePath, string localFilePath)
        {
            switch (isSecureMode)
            {
                case true:
                    using (var outStream = new FileStream(localFilePath, FileMode.Create))
                    {
                        sftpClient.DownloadFile(remoteFilePath, outStream);
                    }
                    break;
                case false:
                    if (ftpClient.DownloadFile(localFilePath,
                                               remoteFilePath,
                                               FtpLocalExists.Resume,
                                               FtpVerify.Retry | FtpVerify.Throw) != FtpStatus.Success)
                        throw new ApplicationException($"Could not download file {remoteFilePath}.");
                    break;
            }
        }

        internal void UploadFile(string remoteFilePath, string localFilePath)
        {
            switch (isSecureMode)
            {
                case true:
                    if (!File.Exists(localFilePath))
                        throw new FileNotFoundException(localFilePath);
                    using (var inStream = new FileStream(localFilePath, FileMode.Open))
                        sftpClient.UploadFile(inStream, remoteFilePath);
                    break;
                case false:
                    if (ftpClient.UploadFile(localFilePath,
                                             remoteFilePath,
                                             FtpRemoteExists.Skip,
                                             false,
                                             FtpVerify.Retry | FtpVerify.Throw) != FtpStatus.Success)
                        throw new ApplicationException($"Could not upload file {remoteFilePath}.");
                    break;
            }
        }

    }
}
