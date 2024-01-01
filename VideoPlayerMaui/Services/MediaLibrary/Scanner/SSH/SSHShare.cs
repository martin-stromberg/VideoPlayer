using Renci.SshNet;
using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace VideoPlayer.Services.MediaLibrary.Scanner.SSH
{
    public class SSHShare
    {

        public SSHShare(string serverName, string username, string password)
        {
            ServerName = serverName;
            Username = username;
            Password = password;
        }

        public string ServerName { get; }

        public string Username { get; }

        public string Password { get; }
        private byte port = 22;

        private SftpClient client = null;

        public void Connect()
        {
            using (var client = new SshClient(ServerName, Username, Password))
            {
                client.HostKeyReceived += (sender, e) =>
                {
                    e.CanTrust = true;
                };
                client.Connect();
            }



            var connectionInfo = new ConnectionInfo(ServerName,
                                                    port,
                                                    Username,
                                                    new PasswordAuthenticationMethod(Username, Password));
            client = new SftpClient(connectionInfo);
            client.ErrorOccurred += Client_ErrorOccurred;
            client.HostKeyReceived += Client_HostKeyReceived;
            client.ServerIdentificationReceived += Client_ServerIdentificationReceived;
            System.Net.ServicePointManager.ServerCertificateValidationCallback = ServerCertificateValidationCallback;

            client.Connect();
        }
        private bool ServerCertificateValidationCallback(object sender,
                                                X509Certificate certificate,
                                                X509Chain chain,
                                                SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
        private void Client_ServerIdentificationReceived(object sender, Renci.SshNet.Common.SshIdentificationEventArgs e)
        {
        }

        private void Client_HostKeyReceived(object sender, Renci.SshNet.Common.HostKeyEventArgs e)
        {
            e.CanTrust = true;
        }

        private void Client_ErrorOccurred(object sender, Renci.SshNet.Common.ExceptionEventArgs e)
        {
            
        }

        public void Disconnect()
        {
            if (client != null)
            {
                if (client.IsConnected)
                    client.Disconnect();
                client.Dispose();
            }
            client = null;
        }

        public bool IsConnected
        {
            get
            {
                return (client != null) && client.IsConnected;
            }
        }

        public IEnumerable<SSHShareFile> ListDirectories(string path)
        {
            return client.ListDirectory(path)
                         .Where(f => f.IsDirectory)
                         .Select(f => new SSHShareFile() { Name = f.Name, Path = f.FullName });
        }

    }
}
