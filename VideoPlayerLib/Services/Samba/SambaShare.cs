using SMBLibrary;
using SMBLibrary.Client;
using VideoPlayerLib.Services.Common;

namespace VideoPlayerLib.Services.Samba
{
    public class SambaShare: RemoteShare
    {
        private string serverName;
        private string username;
        private string password;
        private readonly string[] ignoreFileNames = new string[] { ".", ".." };

        public SambaShare(string serverName, string username, string password)
            :base()
        {
            this.serverName = serverName;
            this.username = username;
            this.password = password;
        }

        private SMB2Client smbClient = null;
        private NTStatus loginStatus = NTStatus.STATUS_USER_SESSION_DELETED;
        private int connectionCount = 0;
        public void Connect()
        {
            if (smbClient == null)
                smbClient = new SMB2Client()
                {
                    
                };
            if (!smbClient.IsConnected)
                try
                {
                    loginStatus = NTStatus.STATUS_USER_SESSION_DELETED;
                    if (!smbClient.Connect(serverName, SMBTransportType.DirectTCPTransport))
                        throw new ApplicationException("Could not connect to smb server.");
                }
                catch
                {
                    return;
                }
            if (loginStatus != NTStatus.STATUS_SUCCESS)
            {
                loginStatus = smbClient.Login("_MSBROWSE_", username, password);
                if (loginStatus != NTStatus.STATUS_SUCCESS)
                    Disconnect();
            }
            CheckConnected();
            connectionCount += 1;
        }
        public void Disconnect()
        {
            connectionCount -= 1;
            if (connectionCount <= 0)
            {
                try
                {
                    var status = smbClient.Logoff();
                    if (status != NTStatus.STATUS_SUCCESS)
                        throw new ApplicationException("Could not logg off from smb server.");
                }
                catch
                {

                }

                connectionCount = 0;
                smbClient.Disconnect();
                loginStatus = NTStatus.STATUS_USER_SESSION_DELETED;
            }
        }
        protected virtual void CheckConnected()
        {
            if (!smbClient.IsConnected)
            {
                Connect();
                if (!smbClient.IsConnected)
                    throw new ApplicationException($"Connection is not established.");
            }
        }
        private void CheckStatusSuccess(NTStatus status, string source)
        {
            if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_NO_MORE_FILES)
                throw new ApplicationException($"Command execution status is {status} {(source == null ? "" : $" (Source: \"{source}\")")}.");
        }

        public string[] Shares
        {
            get
            {
                Connect();
                return ListShares().ToArray();
            }
        }

        public bool IsConnected
        {
            get { return smbClient != null && smbClient.IsConnected; }
        }

        private IEnumerable<string> ListShares()
        {
            CheckConnected();
            List<string> shares = smbClient.ListShares(out var status);
            CheckStatusSuccess(status, null);
            return shares;
        }
        public IEnumerable<FileDirectoryInformation> List(string path)
        {
            CheckConnected();
            path = path.Replace("\\", "/");
            if (path.StartsWith($"//{serverName}/"))
                path = path.Remove(0, $"//{serverName}/".Length);
            path = path.TrimStart('/');
            var offset = path.IndexOf("/");
            var shareName = offset >= 0 ? path.Substring(0, offset) : path;
            path = path.Remove(0, shareName.Length + (offset >= 0 ? 1 : 0));
            ISMBFileStore fileStore = smbClient.TreeConnect(shareName, out var status);
            CheckStatusSuccess(status, shareName);
            try
            {
                object directoryHandle;
                FileStatus fileStatus;
                status = fileStore.CreateFile(out directoryHandle, out fileStatus, path, AccessMask.GENERIC_READ, SMBLibrary.FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
                CheckStatusSuccess(status, path);
                try
                {
                    List<QueryDirectoryFileInformation> fileList;
                    status = fileStore.QueryDirectory(out fileList, directoryHandle, "*", FileInformationClass.FileDirectoryInformation);
                    CheckStatusSuccess(status, $"path/*");
                    return fileList
                        .Cast<FileDirectoryInformation>()
                        .Where(file => !ignoreFileNames.Contains(file.FileName));
                }
                finally
                {
                    status = fileStore.CloseFile(directoryHandle);
                }
            }
            catch(Exception ex)
            {
                throw new ApplicationException($"Could not list {path}.", ex);
            }
            finally
            {
                status = fileStore.Disconnect();
            }
        }
        public IEnumerable<FileDirectoryInformation> ListDirectories(string path)
        {
            return List(path)
                .Where(dir => ((uint)dir.FileAttributes & (uint)SMBLibrary.FileAttributes.Directory) == (uint)SMBLibrary.FileAttributes.Directory);
        }
        public IEnumerable<FileDirectoryInformation> ListFiles(string path)
        {
            return List(path)
                .Where(dir =>
                {
                    var isTemporary = ((uint)dir.FileAttributes & (uint)SMBLibrary.FileAttributes.Normal) == (uint)SMBLibrary.FileAttributes.Temporary;
                    var isDirectory = ((uint)dir.FileAttributes & (uint)SMBLibrary.FileAttributes.Normal) == (uint)SMBLibrary.FileAttributes.Directory;
                    return !isDirectory && !isTemporary;
                });
        }
        public void DownloadFile(string sourcePath, string destPath)
        {
            CheckConnected();
            sourcePath = sourcePath.Replace("\\", "/");
            if (sourcePath.StartsWith($"//{serverName}"))
                sourcePath = sourcePath.Remove(0, 2 + serverName.Length + 1);
            sourcePath = sourcePath.TrimStart('/');
            var offset = sourcePath.IndexOf("/");
            var shareName = offset >= 0 ? sourcePath.Substring(0, offset) : sourcePath;
            sourcePath = sourcePath.Remove(0, shareName.Length + (offset >= 0 ? 1 : 0));
            ISMBFileStore fileStore = smbClient.TreeConnect(shareName, out var status);
            CheckStatusSuccess(status, shareName);
            try
            {
                object fileHandle;
                FileStatus fileStatus;
                status = fileStore.CreateFile(out fileHandle, out fileStatus, sourcePath, AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, SMBLibrary.FileAttributes.Normal, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
                CheckStatusSuccess(status, sourcePath);

                if (!Directory.Exists(Path.GetDirectoryName(destPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                using (FileStream outStream = new FileStream(destPath, FileMode.Create))
                {
                    byte[] data;
                    long bytesRead = 0;
                    while (true)
                    {
                        status = fileStore.ReadFile(out data, fileHandle, bytesRead, (int)smbClient.MaxReadSize);
                        if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                        {
                            throw new Exception("Failed to read from file");
                        }

                        if (status == NTStatus.STATUS_END_OF_FILE || data.Length == 0)
                        {
                            break;
                        }
                        bytesRead += data.Length;
                        outStream.Write(data, 0, data.Length);
                    }
                }
            }
            finally
            {
                status = fileStore.Disconnect();
            }
        }
    }
}
