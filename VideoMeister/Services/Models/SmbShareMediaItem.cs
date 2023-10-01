using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.Samba;

namespace VideoMeister.Services.Models
{
    public class SmbShareMediaItem: MediaItem
    {
        private string internalPath;
        private string username;
        private string password;
        private string serverName;
        private string relativePath;

        public SmbShareMediaItem(SmbShareMediaItemCollection parent, string path, string username, string password)
            : base(parent)
        {
            Path = path;
            path = path.Remove(0, 2);
            internalPath = $"\\\\{path}".Replace("/", "\\");

            this.username = username;
            this.password = password;
            this.serverName = path.Substring(0, path.IndexOf("/"));
            this.relativePath = path.Remove(0, this.serverName.Length + 1);
        }
        private SambaShare smbClient = null;
        protected SambaShare SmbClient
        {
            get
            {
                if (smbClient == null)
                    smbClient = new SambaShare(serverName, username, password);
                return smbClient;
            }
        }

        public string Path { get; }

        public override string URI => internalPath;

        public override string Name => new FileInfo(URI).Name;

        internal void Download(string destPath)
        {
            SmbClient.Connect();
            SmbClient.DownloadFile(URI, destPath);
        }
    }
}
