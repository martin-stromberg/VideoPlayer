
namespace FolderAPI.Models
{
    public class File
    {
        public string Name { get; internal set; }
        public long Size { get; internal set; }
        public DateTime LastWriteTime { get; internal set; }
    }
}
