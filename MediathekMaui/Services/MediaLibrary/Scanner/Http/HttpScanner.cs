using Mediathek.Extensions;
using Mediathek.Services.MediaLibrary.Scanner.Events;
using Mediathek.Services.MediaLibrary.Scanner.Models;
using Mediathek.Services.MediaLibrary.Scanner.Shares;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mediathek.Services.MediaLibrary.Scanner.Http
{
    public class HttpScanner: RemoteSourceScanner
    {

        private HttpShare share;
        private static string[] FolderNameBlacklist = { ".", "..", ".actors" };
        private static string[] FileNameBlacklist = { ".tbn" };
        private bool currentScan_latestScanPathReached = false;
        private string[] currentScan_skipPathParts = null;
        private string currentScan_SkipPath = string.Empty;

        public override bool CanScan(MediaElementSource source)
        {
            return source is HttpMediaSource;
        }

        private HttpMediaSource currentSource = null;

        public override void DownloadFile(string sourceFilePath, string destFilePath)
        {
            sourceFilePath = sourceFilePath.Replace('\\', '/');
            share.DownloadFile(sourceFilePath, destFilePath);
        }

        public override IEnumerable<RemoteFile> FindFiles(
            MediaElementSource source,
            string path,
            string fileMask = "*.*")
        {
            currentSource = (HttpMediaSource)source;
            share = new HttpShare(currentSource.Uri);
            try
            {
                return FindFiles(path, fileMask);
            }
            finally
            {
                currentSource = null;
                share = null;
            }
        }

        public override IEnumerable<RemoteFile> FindFiles(string path, string fileMask = "*.*")
        {
            return share.ListFiles(path.Replace('\\', '/'))
                        .Where(f => !FileNameBlacklist.Contains(f.Name))
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
                                new HttpShareFile()
                        {
                            Name = f.Name,
                            Path = Path.Combine(path, f.Name).Replace("\\", "/"),
                            LastWriteTime = f.LastWriteTime
                        });
        }

        public override IEnumerable<RemoteFolder> FindFolders(
            RemoteMediaSource source,
            string path,
            string folderNameMask)
        {
            bool ownShare = share == null;
            if (ownShare)
                share = new HttpShare(((HttpMediaSource)source).Uri);
            try
            {
                return share.ListDirectories(path.Replace('\\', '/'))
                            .Where(f =>
                            {
                                Regex mask = new Regex(
                                    '^' +
                                    folderNameMask
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
                                    new HttpShareFolder()
                            {
                                Name = f.Name,
                                Path = Path.Combine(path, f.Name).Replace("\\", "/")
                            });
            }
            finally
            {
                share = null;
            }
        }

        public override string ReadTextFile(string filePath)
        {
            filePath = filePath.Replace('\\', '/');
            var tempFolder = Directory.CreateTempSubdirectory();
            try
            {
                var fileName = Path.GetFileName(filePath);
                var tempFile = Path.Combine(tempFolder.FullName, fileName);
                share.DownloadFile(filePath, tempFile);
                return File.ReadAllText(tempFile);
            }
            finally
            {
                tempFolder.Delete(true);
            }
        }

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

        public override void Scan(MediaElementSource source, bool noContinue)
        {
            currentSource = (HttpMediaSource)source;
            share = new HttpShare(currentSource.Uri);
            try
            {
                CurrentSource = source as RemoteMediaSource;
                if (noContinue)
                    CurrentSource.LatestScanPath = string.Empty;

                currentScan_SkipPath = string.Empty;
                currentScan_skipPathParts = null;
                if (!string.IsNullOrWhiteSpace(currentSource.LatestScanPath))
                {
                    currentScan_SkipPath = currentSource.LatestScanPath?.Remove(0, currentSource.Path.Length);
                    currentScan_skipPathParts = currentSource.LatestScanPath?.Remove(0, currentSource.Path.Length)
                                                                             .Split(currentSource.PathDelimiter);
                }
                currentScan_latestScanPathReached = currentScan_skipPathParts == null;

                Scan(currentSource.Path, false);
                OnScanCompleted();
            }
            finally
            {
                CurrentSource = null;
                currentSource = null;
                share = null;
            }
        }

        private void Scan(string path, bool isSubFolder)
        {
            var args = new FolderScanEventArgs(path) { Success = false };
            try
            {
                OnBeforeScanFolder(args);
                var files = args.ScanFiles ? FindFiles(path).Select(mediaItem => OnMediaItemFound(mediaItem)).ToArray() : null;
                var folders = args.ScanFolders ? share.ListDirectories(path)
                                                      .Where(f => !FolderNameBlacklist.Contains(f.Name))
                                                      .ToArray()
                                                      .Select(f =>
                                                              new HttpShareFolder()
                                                      {
                                                          Name = f.Name,
                                                          Path = Path.Combine(path, f.Name).Replace("\\", "/"),
                                                          LastWriteTime = f.LastWriteTime
                                                      })
                                                      .Select(folder => OnFolderFound(folder))
                                                      .Select(folder =>
                                                      {
                                                          Scan(folder.Path, true);
                                                          return folder;
                                                      })
                                                      .ToArray() : null;
                args.Success = true;
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
            finally
            {
                OnAfterScanFolder(args);
            }
        }

        public override void Scan(MediaElementSource source, MediaItem mediaItem)
        {
            throw new NotImplementedException();
        }

        public override bool TestConnection(MediaElementSource mediaSource)
        {
            HttpMediaSource source = (HttpMediaSource)mediaSource;
            var share = new HttpShare(source.Uri);
            share.TestConnection();
            return true;
        }

        public override void WriteTextFile(string nfoPath, string innerXml)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, innerXml);
                share.UploadFile(nfoPath, tempFile, true);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        internal override void SavePictureFromUri(string imageURL, string imageFilePath)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
                HttpClient client = new HttpClient();
                using (var inStream = client.GetStreamAsync(imageURL).Wait<Stream>())
                    using (var fs = new FileStream(tempFile, FileMode.CreateNew))
                    {
                        inStream.CopyToAsync(fs).Wait();
                    }
                share.UploadFile(imageFilePath, tempFile, false);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

    }
}
