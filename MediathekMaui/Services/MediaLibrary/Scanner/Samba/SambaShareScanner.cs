using Mediathek.Extensions;
using Mediathek.Services.MediaLibrary.Scanner.Events;
using Mediathek.Services.MediaLibrary.Scanner.Models;
using Mediathek.Services.MediaLibrary.Scanner.Shares;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mediathek.Services.MediaLibrary.Scanner.Samba
{
    public class SambaShareScanner: RemoteSourceScanner
    {

        private SambaShare share;
        private static string[] FolderNameBlacklist = { "$RECYCLE.BIN", "System Volume Information", "lost+found" };

        public SambaShareScanner() { }

        protected override RemoteFolder OnFolderFound(RemoteFolder folder)
        {
            OnFolderFound(new SmbShareFolderEventArgs((SmbShareFolder)folder));
            return folder;
        }

        protected override RemoteFile OnMediaItemFound(RemoteFile file)
        {
            OnMediaItemFound(new SmbShareFileEventArgs((SmbShareFile)file));
            return file;
        }

        public override bool CanScan(MediaElementSource source)
        {
            return source is SmbMediaSource;
        }

        public override void Scan(MediaElementSource source, bool noContinue)
        {
            SmbMediaSource mediaSource = (SmbMediaSource)source;
            share = new SambaShare(mediaSource.ServerName, mediaSource.Username, mediaSource.Password);
            try
            {
                CurrentSource = source as RemoteMediaSource;
                if (noContinue)
                    CurrentSource.LatestScanPath = string.Empty;
                Scan(mediaSource.Path, false);
            }
            finally
            {
                CurrentSource = null;
                share = null;
            }
        }

        public override void Scan(MediaElementSource source, MediaItem mediaItem)
        {
            SmbMediaSource mediaSource = (SmbMediaSource)source;
            share = new SambaShare(mediaSource.ServerName, mediaSource.Username, mediaSource.Password);
            try
            {
                CurrentSource = source as RemoteMediaSource;
                share.Connect();
                try
                {
                    var folderPath = Path.GetDirectoryName(mediaItem.Path);
                    var fileName = Path.GetFileName(mediaItem.Path);
                    var files = share.ListFiles(folderPath)
                                     .Where(f => f.FileName == fileName)
                                     .ToArray()
                                     .Select(f =>
                                             new SmbShareFile()
                                     {
                                         Name = f.FileName,
                                         Path = Path.Combine(folderPath, f.FileName).Replace("/", "\\")
                                     })
                                     .Select(mediaItem => OnMediaItemFound(mediaItem))
                                     .ToArray();
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
                var files = args.ScanFiles ? share.ListFiles(path)
                                                  .ToArray()
                                                  .Select(f =>
                                                          new SmbShareFile()
                                                  {
                                                      Name = f.FileName,
                                                      Path = Path.Combine(path, f.FileName).Replace("/", "\\")
                                                  })
                                                  .Select(mediaItem => OnMediaItemFound(mediaItem))
                                                  .ToArray() : null;
                var folders = args.ScanFolders ? share.ListDirectories(path)
                                                      .Where(f => !FolderNameBlacklist.Contains(f.FileName))
                                                      .ToArray()
                                                      .Select(f =>
                                                              new SmbShareFolder()
                                                      {
                                                          Name = f.FileName,
                                                          Path = Path.Combine(path, f.FileName).Replace("/", "\\")
                                                      })
                                                      .Select(folder => OnFolderFound(folder))
                                                      .Select(folder =>
                                                      {
                                                          Scan(folder.Path, true);
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
                return share.ListFiles(path)
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
                                return mask.IsMatch(f.FileName);
                            })
                            .ToArray()
                            .Select(f =>
                                    new SmbShareFile()
                            {
                                Name = f.FileName,
                                Path = Path.Combine(path, f.FileName).Replace("/", "\\")
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
                    DownloadFile(filePath, tempFile);
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

        public override void WriteTextFile(string nfoPath, string innerXml)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<RemoteFile> FindFiles(
            MediaElementSource source,
            string path,
            string fileMask = "*.*")
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<RemoteFolder> FindFolders(
            RemoteMediaSource source,
            string path,
            string folderNameMask)
        {
            throw new NotImplementedException();
        }

        public override bool TestConnection(MediaElementSource mediaSource)
        {
            throw new NotImplementedException();
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

                        // share.UploadFile(imageFilePath, tempFile);
                        throw new NotImplementedException();
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
