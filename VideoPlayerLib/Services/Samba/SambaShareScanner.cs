using Microsoft.Maui.ApplicationModel.DataTransfer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Common;
using VideoPlayerLib.Services.Database.Models;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace VideoPlayerLib.Services.Samba
{
    public class SambaShareScanner: RemoteSourceScanner
    {
        private readonly SambaShare share;
        private static string[] FolderNameBlacklist = { "$RECYCLE.BIN", "System Volume Information", "lost+found" };

        public SambaShareScanner(SambaShare share)
        {
            this.share = share;
        }

        protected override Folder OnFolderFound(Folder folder)
        {
            OnFolderFound(new SmbShareFolderEventArgs((SmbShareFolder)folder));
            return folder;
        }
        protected override Common.RemoteFile OnMediaItemFound(Common.RemoteFile file)
        {
            OnMediaItemFound(new SmbShareFileEventArgs((SmbShareFile)file));
            return file;
        }

        public override void Scan(string path)
        {
            Scan(path, false);
        }
        private void Scan(string path, bool isSubFolder)
        {
            if (!isSubFolder)
                share.Connect();
            try
            {
                var files = share.ListFiles(path)
                    .ToArray()
                    .Select(f => new SmbShareFile()
                    {
                        Name = f.FileName,
                        Path = Path.Combine(path, f.FileName).Replace("/", "\\")
                    })
                    .Select(mediaItem => OnMediaItemFound(mediaItem))
                    .ToArray();
                var folders = share.ListDirectories(path)
                    .Where(f => !FolderNameBlacklist.Contains(f.FileName))
                    .ToArray()
                    .Select(f => new SmbShareFolder()
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
                    .ToArray();                    
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
                        .Select(f => new SmbShareFile()
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
    }
}
