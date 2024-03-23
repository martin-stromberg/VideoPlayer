using FolderAPI.Models;
using System.Text;

namespace FolderAPI.Services
{
    public class FileManager
    {

        private readonly string[] FolderBlacklist = new string[] { "$RECYCLE.BIN", "System Volume Information" };
        private readonly string[] FileBlacklist = new string[] { string.Empty };
        private readonly string[] FileExtBlacklist = new string[] { string.Empty };
        private Dictionary<string, string> shares = new Dictionary<string, string>()
        {
            { "MediaServer", "\\\\raspberrypi\\FileServer\\" }
        };
        private List<string> PathBlacklist = new List<string>()
        {
            "\\\\raspberrypi\\FileServer\\Crucial X62\\Dokumente",
            "\\\\raspberrypi\\FileServer\\Crucial X62\\Backup",
            "\\\\raspberrypi\\FileServer\\Crucial X62\\Bilder",
            "\\\\raspberrypi\\FileServer\\Crucial X62\\Zu konvertieren"
        };
        private readonly string[] ImageFileExts = new string[] { ".jpg", ".png" };

        public Folder GetFolder(string path = "")
        {
            if (string.IsNullOrWhiteSpace(path) || (path == "/"))
                return new Folder()
                {
                    Directories = shares.Select(x => new FolderInfo() { Name = x.Key }).ToArray(),
                    Files = null
                };
            var parts = path.Split('/').Skip(1).ToArray();
            var shareName = parts.FirstOrDefault();
            return GetShareFolder(shareName, parts.Skip(1).ToArray());
        }

        internal string GetFilePath(string path)
        {
            var parts = path.Split('/').Skip(1).ToArray();
            var shareName = parts.FirstOrDefault();
            return $"{shares[shareName]}{string.Join("/", parts.Skip(1))}";
        }

        internal void SaveFile(Stream strm, string savePath, bool overwrite, bool isTextFile)
        {
            var parts = savePath.Split('/').Skip(1).ToArray();
            var shareName = parts.FirstOrDefault();
            var path = $"{shares[shareName]}{string.Join("\\", parts.Skip(1))}";
            if (System.IO.File.Exists(path))
            {
                if (!overwrite)
                    throw new ApplicationException($"the file already exists.");
                System.IO.File.Delete(path);
            }
            if (isTextFile)
            {
                using (var reader = new StreamReader(strm, Encoding.UTF8))
                    using (StreamWriter writer = new StreamWriter(new FileStream(path, FileMode.CreateNew), Encoding.UTF8))
                        writer.Write(reader.ReadToEnd());
            }
            else
            {
                using (var fs = new FileStream(path, FileMode.CreateNew))
                {
                    strm.CopyToAsync(fs).Wait();
                }
            }
        }

        private Folder GetShareFolder(string? shareName, string[] pathParts)
        {
            var path = $"{shares[shareName]}{string.Join("/", pathParts)}";
            var isRoot = string.IsNullOrWhiteSpace(pathParts.FirstOrDefault());
            DirectoryInfo folder = new DirectoryInfo(path);
            if (!folder.Exists)
                return null;
            return new Folder()
            {
                Directories = GetChildFolders(folder)
                              .Where(x => !FolderBlacklist.Contains(x.Name))
                              .Select(x =>
                                      new FolderInfo()
                    {
                        Name = x.Name,
                        LastWriteTime = GetFolderWriteTime(x, isRoot ? 1 : int.MaxValue)
                    })
                              .ToArray(),
                Files = folder
                    .GetFiles()
                    .Where(x => !FileBlacklist.Contains(x.Name))
                    .Where(x => !FileExtBlacklist.Contains(x.Extension))
                    .Select(x => new Models.File() { Name = x.Name, Size = x.Length, LastWriteTime = x.LastWriteTime })
                    .ToArray()
            };
        }

        private DateTime GetFolderWriteTime(DirectoryInfo folder, int level = 0)
        {
            var lastWriteTime = folder.LastWriteTime;
            try
            {
                if (level > 0)
                    foreach (var subDir in GetChildFolders(folder))
                    {
                        var subDirTime = GetFolderWriteTime(subDir, level - 1);
                        if (subDirTime > lastWriteTime)
                            lastWriteTime = subDirTime;
                    }
                foreach (var file in folder.GetFiles())
                    if (file.LastWriteTime > lastWriteTime)
                        lastWriteTime = file.LastWriteTime;
            }
            catch (UnauthorizedAccessException) { }
            return lastWriteTime;
        }

        private IEnumerable<DirectoryInfo> GetChildFolders(DirectoryInfo folder)
        {
            return folder.GetDirectories().Where(f => !PathBlacklist.Contains(f.FullName));
        }

    }
}
