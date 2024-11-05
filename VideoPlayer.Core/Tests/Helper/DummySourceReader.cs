using System;
using System.Linq;
using VideoPlayer.Extensions;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.SourceReader;

namespace VideoPlayer.Tests.Helper
{
    public class DummySourceReader: ISourceReader
    {

        private Dictionary<string, string> _Structure = new Dictionary<string, string>();
        private string name;

        public DummySourceReader(string name)
        {
            this.name = name;
        }

        public void AddFile(string path)
        {
            if (_Structure.ContainsKey(path))
                throw new ApplicationException($"Key {path} already exists.");
            _Structure.Add(path, path);
        }

        public void AddFolder(string path)
        {
            if (_Structure.ContainsKey(path))
                throw new ApplicationException($"Key {path} already exists.");
            _Structure.Add(path, path);
        }

        public void Clear()
        {
            _Structure.Clear();
        }

        public FileInfo Download(MediaItem nfoFile, Action<decimal> progressCallback)
        {
            string localFilePath = Path.GetTempFileName();
            File.Delete(localFilePath);
            var localFolderPath = Path.GetDirectoryName(localFilePath);
            if (!Path.Exists(localFolderPath))
                Directory.CreateDirectory(localFolderPath);
            long fileSize = -1;
            decimal currectProgress = 0;
            IProgress<long> progress = new Progress<long>(value =>
            {
                decimal tmp = (decimal)(value * 100) / fileSize;
                if (tmp != currectProgress && tmp > currectProgress)
                {
                    currectProgress = tmp;
                    progressCallback(tmp);
                }
            });
            string remoteFilePath = nfoFile.Path.Replace("\\", "-").Replace("/", "-").Replace(":", "-");
            using (var stream = FindFileStream(remoteFilePath))
                using (var fileStream = new FileStream(localFilePath, FileMode.CreateNew))
                    stream.CopyToAsync(fileStream, progress, CancellationToken.None).Wait();
            return new FileInfo(localFilePath);
        }

        public SourceFolder GetRoot()
        {
            return new SourceFolder() { FullPath = "/", Path = "/", Name = name };
        }

        public Task<IEnumerable<SourceFile>> ReadFilesAsync(SourceFolder folder)
        {
            return Task.FromResult(_Structure
                .Select(s =>
                {
                    if (!s.Key.StartsWith(folder.Path))
                        return null;
                    var relPath = s.Key.Substring(folder.Path.Length).TrimStart('/');
                    if (relPath.Contains('/'))
                        return null;
                    if (relPath.Contains('\\'))
                        return null;
                    var ext = Path.GetExtension(relPath);
                    if (string.IsNullOrWhiteSpace(ext))
                        return null;
                    return new SourceFile()
                    {
                        FullPath = $"{folder.FullPath}/{relPath}",
                        Name = relPath,
                        LastWriteTime = DateTime.Now,
                        Path = $"{folder.Path}/{relPath}"
                    };
                })
                .Where(s => s is not null));
        }

        public Task<IEnumerable<SourceFolder>> ReadFoldersAsync(SourceFolder folder)
        {
            return Task.FromResult(_Structure
                .Select(s =>
                {
                    if (!s.Key.StartsWith(folder.Path))
                        return null;
                    var relPath = s.Key.Substring(folder.Path.Length);
                    if (relPath.Length == 0)
                        return null;
                    if (relPath.TrimStart('/').Contains('/'))
                        return null;
                    if (relPath.TrimStart('/').Contains('\\'))
                        return null;
                    var ext = Path.GetExtension(relPath);
                    if (!string.IsNullOrWhiteSpace(ext))
                        return null;
                    return new SourceFolder()
                    {
                        FullPath = $"{folder.FullPath}{relPath}",
                        Name = relPath,
                        LastWriteTime = DateTime.Now,
                        Path = $"{folder.Path}{relPath}"
                    };
                })
                .Where(s => s is not null));
        }

        private Stream FindFileStream(string fileName, bool findLink = true)
        {
            try
            {
                var stream = FileSystem.OpenAppPackageFileAsync(fileName);
                return stream.Result;
            }
            catch
            {
                var linkFile = $"{fileName}.lnk";
                using (var stream = FindFileStream(linkFile))
                    if (stream is not null)
                    {
                        using var reader = new StreamReader(stream);
                        fileName = reader.ReadToEnd();
                        return FindFileStream(fileName, findLink);
                    }
            }
            return null;
        }

        public string ReadTextFile(MediaItem nfoFile)
        {
            string fileName = nfoFile.Path.Replace("\\", "-").Replace("/", "-").Replace(":", "-");
            using (var stream = FindFileStream(fileName))
                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();
                    return content;
                }
        }

        public Task<SourceFile> ReadFileAsync(MediaItem mediaItem)
        {
            return Task.FromResult(_Structure
                .Select(s =>
                {
                    if (!s.Key.StartsWith(mediaItem.Path))
                        return null;
                    return new SourceFile()
                    {
                        FullPath = $"{mediaItem.Path}",
                        Name = mediaItem.Name,
                        LastWriteTime = DateTime.Now,
                        Path = $"{mediaItem.Path}"
                    };
                })
                .FirstOrDefault(s => s is not null));
        }

    }
}
