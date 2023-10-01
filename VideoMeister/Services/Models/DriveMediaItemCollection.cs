using VideoMeister.Services.VideoSources;

namespace VideoMeister.Services.Models
{
    public class DriveMediaItemCollection : MediaItemCollection
    {
        private string path;

        public DriveMediaItemCollection(VideoSource source, string path) 
            : base(source)
        {
            this.path = path;
        }
        private MediaItemCollection[] folders = null;
        public override MediaItemCollection[] Folders
        {
            get
            {
                if (folders == null)
                {
                    folders = new DirectoryInfo(path)
                        .GetDirectories()
                        .Select(d => new DriveMediaItemCollection(Source, $"{path}\\{d.Name}"))
                        .ToArray();
                }
                return folders;
            }
        }

        private MediaItem[] files = null;
        public override MediaItem[] Files
        {
            get
            {
                if (files == null)
                {                    
                    files = new DirectoryInfo(path)
                        .GetFiles()
                        .Select(f => new DriveMediaItem(this, $"{path}\\{f.Name}"))
                        .ToArray();
                }
                return files;
            }
        }

        public override string Name => new DirectoryInfo(path).Name;

        public string URI => new DirectoryInfo(path).FullName;

        public override void Refresh()
        {
            files = null;
            folders = null;
        }

        public override DriveMediaItemCollection CreateFolder(string name)
        {
            var folder = (DriveMediaItemCollection)Folders.FirstOrDefault(f => f.Name == name);
            if (folder != null)
                return folder;
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            directoryInfo.CreateSubdirectory($"{name}");
            folders = null;
            folder = (DriveMediaItemCollection)Folders.FirstOrDefault(f => f.Name == name);
            return folder;
        }
    }
}
