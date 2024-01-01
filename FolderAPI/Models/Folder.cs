namespace FolderAPI.Models
{
    public class Folder
    {
        public FolderInfo[] Directories { get; internal set; }
        public File[] Files { get; internal set; }
    }

    public class FolderInfo
    {
        public string Name { get; internal set; }
        public DateTime LastWriteTime { get; internal set; }
    }
}
