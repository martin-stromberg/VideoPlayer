using System;
using System.Linq;
using System.Text.RegularExpressions;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Sources;
using VideoPlayer.Services.MediaLibrary.Scanner.FTP;
using VideoPlayer.Services.MediaLibrary.Scanner.Models;
using VideoPlayer.Services.MediaLibrary.Scanner.Shares;

namespace VideoPlayer.Services.MediaLibrary.Scanner.SSH
{
    internal class SSHScanner: RemoteSourceScanner
    {

        private SSHShare share = null;

        public override bool CanScan(MediaSource source)
        {
            return source is SSHMediaSource;
        }

        public override void DownloadFile(string sourceFilePath, string destFilePath)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<RemoteFile> FindFiles(MediaSource source, string path, string fileMask = "*.*")
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<RemoteFile> FindFiles(string path, string fileMask = "*.*")
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
            throw new NotImplementedException();
        }

        public override void Scan(MediaSource source)
        {
            throw new NotImplementedException();
        }

        public override void Scan(MediaSource source, MediaItem mediaItem)
        {
            throw new NotImplementedException();
        }

        public override bool TestConnection(MediaSource mediaSource)
        {
            SSHMediaSource source = mediaSource as SSHMediaSource;
            share = new SSHShare(source.ServerName, source.Username, source.Password);
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

        public override void WriteTextFile(string nfoPath, string innerXml)
        {
            throw new NotImplementedException();
        }

    }
}
