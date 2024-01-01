using System;
using System.Linq;
using System.Text.RegularExpressions;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Sources;
using VideoPlayer.Services.MediaLibrary.Scanner.Events;
using VideoPlayer.Services.MediaLibrary.Scanner.Models;
using VideoPlayer.Services.MediaLibrary.Scanner.Shares;

namespace VideoPlayer.Services.MediaLibrary.Scanner.Samba
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

        public override bool CanScan(MediaSource source)
        {
            return source is SmbMediaSource;
        }

        public override void Scan(MediaSource source)
        {
            SmbMediaSource mediaSource = (SmbMediaSource)source;
            share = new SambaShare(mediaSource.ServerName, mediaSource.Username, mediaSource.Password);
            try
            {
                CurrentSource = source as RemoteMediaSource;
                Scan(mediaSource.Path, false);
            }
            finally
            {
                CurrentSource = null;
                share = null;
            }
        }

        public override void Scan(MediaSource source, MediaItem mediaItem)
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

        public override IEnumerable<RemoteFile> FindFiles(MediaSource source, string path, string fileMask = "*.*")
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

        public override bool TestConnection(MediaSource mediaSource)
        {
            throw new NotImplementedException();
        }

    }
}
