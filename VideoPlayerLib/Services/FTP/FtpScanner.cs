using System;
using System.Linq;
using System.Text.RegularExpressions;
using VideoPlayerLib.Services.Common;

namespace VideoPlayerLib.Services.FTP
{
    public class FtpScanner: RemoteSourceScanner
    {

        private FtpShare share;
        private static string[] FolderNameBlacklist = { ".", ".." };

        public FtpScanner(
            FtpShare share)
        {
            this.share = share;
        }

        public override void Scan(string path)
        {
            ScanInternal(path, false);
        }

        private void ScanInternal(string path, bool isSubFolder)
        {
            if (!isSubFolder)
                share.Connect();
            try
            {
                var args = new FolderScanEventArgs(path);
                OnBeforeScanFolder(args);
                var files = args.ScanFiles ? FindFiles(path).Select(mediaItem => OnMediaItemFound(mediaItem)).ToArray() : null;
                var folders = args.ScanFolders ? share.ListDirectories(path)
                                                      .Where(f => !FolderNameBlacklist.Contains(f.Name))
                                                      .ToArray()
                                                      .Select(f =>
                                                              new FtpShareFolder()
                                                      {
                                                          Name = f.Name,
                                                          Path = Path.Combine(path, f.Name).Replace("\\", "/")
                                                      })
                                                      .Select(folder => OnFolderFound(folder))
                                                      .Select(folder =>
                                                      {
                                                          ScanInternal(folder.Path, true);
                                                          return folder;
                                                      })
                                                      .ToArray() : null;
            }
            finally
            {
                if (!isSubFolder)
                    share.Disconnect();
            }
        }

        public override IEnumerable<RemoteFile> FindFiles(string path, string fileMask = "*.*")
        {
            bool wasConnected = share.IsConnected;
            if (!wasConnected)
                share.Connect();
            try
            {
                return share.ListFiles(path.Replace('\\', '/'))
                            .Where(f =>
                            {
                                Regex mask = new Regex(
                                '^' +
                                fileMask
                                .Replace(".", "[.]")
                                .Replace("*", ".*")
                                .Replace("?", ".")
                                                        + '$',
                                RegexOptions.IgnoreCase);
                                return mask.IsMatch(f.Name);
                            })
                            .ToArray()
                            .Select(f =>
                                    new FtpShareFile()
                            {
                                Name = f.Name,
                                Path = Path.Combine(path, f.Name).Replace("\\", "/")
                            });
            }
            finally
            {
                if (!wasConnected)
                    share.Disconnect();
            }
        }

        public override string ReadTextFile(string filePath)
        {
            filePath = filePath.Replace('\\', '/');
            var tempFolder = Directory.CreateTempSubdirectory();
            try
            {
                bool wasConnected = share.IsConnected;
                if (!wasConnected)
                    share.Connect();
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    var tempFile = Path.Combine(tempFolder.FullName, fileName);
                    share.DownloadFile(filePath, tempFile);
                    return File.ReadAllText(tempFile);
                }
                finally
                {
                    if (!wasConnected)
                        share.Disconnect();
                }
            }
            finally
            {
                tempFolder.Delete(true);
            }
        }

        public override void DownloadFile(string sourceFilePath, string destFilePath)
        {
            sourceFilePath = sourceFilePath.Replace('\\', '/');
            bool wasConnected = share.IsConnected;
            if (!wasConnected)
                share.Connect();
            try
            {
                share.DownloadFile(sourceFilePath, destFilePath);
            }
            finally
            {
                if (!wasConnected)
                    share.Disconnect();
            }
        }

        public override void WriteTextFile(string filePath, string text)
        {
            filePath = filePath.Replace('\\', '/');
            var tempFile = Path.GetTempFileName();
            try
            {
                bool wasConnected = share.IsConnected;
                if (!wasConnected)
                    share.Connect();
                try
                {
                    File.WriteAllText(tempFile, text);
                    share.UploadFile(filePath, tempFile);
                }
                finally
                {
                    if (!wasConnected)
                        share.Disconnect();
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

    }
}
