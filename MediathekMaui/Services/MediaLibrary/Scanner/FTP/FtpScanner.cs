using Mediathek.Extensions;
using Mediathek.Services.MediaLibrary.Scanner.Events;
using Mediathek.Services.MediaLibrary.Scanner.Models;
using Mediathek.Services.MediaLibrary.Scanner.Shares;
using System.Text.RegularExpressions;

namespace Mediathek.Services.MediaLibrary.Scanner.FTP
{
    public class FtpScanner: RemoteSourceScanner
    {

        private FtpShare share;
        private static string[] FolderNameBlacklist = { ".", ".." };
        private bool currentScan_latestScanPathReached = false;
        private string[] currentScan_skipPathParts = null;
        private string currentScan_SkipPath = string.Empty;

        public FtpScanner() { }

        protected override void OnBeforeScanFolder(FolderScanEventArgs e)
        {
            var isParentLevelFolder = false;
            if (!currentScan_latestScanPathReached)
                if (currentScan_skipPathParts == null)
                    currentScan_latestScanPathReached = true;
                else
                {
                    var source = CurrentSource as RemoteMediaSource;
                    var currRelPath = e.Value.Remove(0, source.Path.Length);
                    isParentLevelFolder = currentScan_SkipPath.StartsWith(currRelPath);
                    currentScan_latestScanPathReached = currentScan_SkipPath == currRelPath;
                }

            e.ScanFolders = currentScan_latestScanPathReached || isParentLevelFolder;
            e.ScanFiles = currentScan_latestScanPathReached;
            base.OnBeforeScanFolder(e);
        }

        public override bool CanScan(MediaElementSource source)
        {
            return source is FtpMediaSource;
        }

        public override void Scan(MediaElementSource source, bool noContinue)
        {
            FtpMediaSource mediaSource = (FtpMediaSource)source;
            share = new FtpShare(mediaSource.ServerName, mediaSource.Username, mediaSource.Password);
            try
            {
                CurrentSource = source as RemoteMediaSource;
                if (noContinue)
                    CurrentSource.LatestScanPath = string.Empty;

                currentScan_SkipPath = string.Empty;
                currentScan_skipPathParts = null;
                if (!string.IsNullOrWhiteSpace(mediaSource.LatestScanPath))
                {
                    currentScan_SkipPath = mediaSource.LatestScanPath?.Remove(0, mediaSource.Path.Length);
                    currentScan_skipPathParts = mediaSource.LatestScanPath?.Remove(0, mediaSource.Path.Length)
                                                                           .Split(mediaSource.PathDelimiter);
                }
                currentScan_latestScanPathReached = currentScan_skipPathParts == null;

                Scan(mediaSource.Path, false);
                OnScanCompleted();
            }
            finally
            {
                CurrentSource = null;
                share = null;
            }
        }

        public override void Scan(MediaElementSource source, MediaItem mediaItem)
        {
            FtpMediaSource mediaSource = (FtpMediaSource)source;
            share = new FtpShare(mediaSource.ServerName, mediaSource.Username, mediaSource.Password);
            try
            {
                CurrentSource = source as RemoteMediaSource;

                var folderPath = Path.GetDirectoryName(mediaItem.Path);
                share.Connect();
                try
                {
                    var files = FindFiles(folderPath)
                                .Where(mI => mI.Path == mediaItem.Path)
                                .Select(mediaItem => OnMediaItemFound(mediaItem))
                                .ToArray();
                }
                catch (Exception ex)
                {
                    OnError(ex);
                }
                finally
                {
                    share.Disconnect();
                }
            }
            finally
            {
                CurrentSource = null;
                share = null;
            }
        }

        private void Scan(string path, bool isSubFolder)
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
                                                          Scan(folder.Path, true);
                                                          return folder;
                                                      })
                                                      .ToArray() : null;
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
            finally
            {
                if (!isSubFolder)
                    share.Disconnect();
            }
        }

        public override IEnumerable<RemoteFile> FindFiles(
            MediaElementSource source,
            string path,
            string fileMask = "*.*")
        {
            FtpMediaSource mediaSource = (FtpMediaSource)source;
            share = new FtpShare(mediaSource.ServerName, mediaSource.Username, mediaSource.Password);
            try
            {
                return FindFiles(path, fileMask);
            }
            finally
            {
                CurrentSource = null;
                share = null;
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
                                .Replace("(", "\\(")
                                .Replace(")", "\\)")
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

        public override IEnumerable<RemoteFolder> FindFolders(
            RemoteMediaSource source,
            string path,
            string folderNameMask)
        {
            FtpMediaSource mediaSource = (FtpMediaSource)source;
            share = new FtpShare(mediaSource.ServerName, mediaSource.Username, mediaSource.Password);
            try
            {
                return FindFolders(path, folderNameMask);
            }
            finally
            {
                CurrentSource = null;
                share = null;
            }
        }

        private IEnumerable<RemoteFolder> FindFolders(string path, string folderMask = "*")
        {
            bool wasConnected = share.IsConnected;
            if (!wasConnected)
                share.Connect();
            try
            {
                return share.ListDirectories(path.Replace('\\', '/'))
                            .Where(f =>
                            {
                                Regex mask = new Regex(
                                '^' +
                                folderMask
                                .Replace(".", "[.]")
                                .Replace("*", ".*")
                                .Replace("?", ".")
                                .Replace("(", "\\(")
                                .Replace(")", "\\)")
                                                        + '$',
                                RegexOptions.IgnoreCase);
                                return mask.IsMatch(f.Name);
                            })
                            .ToArray()
                            .Select(f =>
                                    new FtpShareFolder()
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

        public override bool TestConnection(MediaElementSource source)
        {
            FtpMediaSource mediaSource = (FtpMediaSource)source;
            share = new FtpShare(mediaSource.ServerName, mediaSource.Username, mediaSource.Password);
            try
            {
                CurrentSource = source as RemoteMediaSource;
                var folders = FindFolders(string.Empty).ToArray();
                return true;
            }
            finally
            {
                CurrentSource = null;
                share = null;
            }
        }

        internal override void SavePictureFromUri(string imageURL, string imageFilePath)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                HttpClient client = new HttpClient();
                using (var inStream = client.GetStreamAsync(imageURL).Wait<Stream>())
                    using (var fs = new FileStream(tempFile, FileMode.CreateNew))
                    {
                        inStream.CopyToAsync(fs).Wait();
                        share.UploadFile(imageFilePath, tempFile);
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
