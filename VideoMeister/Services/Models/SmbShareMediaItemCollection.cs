using SMBLibrary;
using SMBLibrary.Client;
using VideoMeister.Services.Samba;
using VideoMeister.Services.VideoSources;

namespace VideoMeister.Services.Models
{
    public class SmbShareMediaItemCollection : MediaItemCollection
    {
        public SmbShareMediaItemCollection(SmbShareVideoSource source, string path, string username, string password)
            : base(source)
        {
            path = path.Replace("\\", "/");
            if (path.EndsWith("/"))
                path = path.Remove(path.Length - 1);
            this.username = username;
            this.password = password;
            Path = path;
            path = path.Remove(0, 2);
            this.serverName = path.Substring(0, path.IndexOf("/"));
            this.relativePath = path.Remove(0, this.serverName.Length + 1);
            //this.shareName = path.Substring(serverName.Length + 1);
            //if (this.shareName.IndexOf("/") >= 0)
            //this.shareName = this.shareName.Remove(this.shareName.IndexOf("/"));
            //Path = path.Remove(0, this.serverName.Length + this.shareName.Length + 1);
            //if (Path.StartsWith("/"))
            //Path = Path.Remove(0, 1);
        }

        private string serverName;
        private string relativePath;

        //private string shareName;
        public string Path { get; }

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


        private MediaItemCollection[] folders = null;
        public MediaItem[] files = null;
        private string username;
        private string password;
        public override MediaItem[] Files
        {
            get
            {
                if (files == null)
                {
                    SmbClient.Connect();
                    try
                    {
                        files = SmbClient.ListFiles(relativePath)
                            .Select(f => new SmbShareMediaItem(this, $"{Path}/{f.FileName}", username, password))
                            .ToArray();
                    }
                    finally
                    {
                        SmbClient.Disconnect();
                    }
                }       
                return files;
            }
        }
        public override MediaItemCollection[] Folders
        {
            get
            {
                if (folders == null)
                {
                    SmbClient.Connect();
                    try
                    {
                        folders = SmbClient.ListDirectories(Path)
                                    .Select(file => new SmbShareMediaItemCollection(Source as SmbShareVideoSource, $"{Path}/{file.FileName}", username, password))
                                    .ToArray();
                    }
                    finally
                    {
                        SmbClient.Disconnect();
                    }
                }
                return folders;
            }
        }

        public override string Name
        {
            get
            {
                return Path.Split('/').Last();
            }
        }

        public override void Refresh()
        {
            files = null;
            folders = null;
        }

        public override DriveMediaItemCollection CreateFolder(string name)
        {
            throw new NotImplementedException();
        }
    }
}
