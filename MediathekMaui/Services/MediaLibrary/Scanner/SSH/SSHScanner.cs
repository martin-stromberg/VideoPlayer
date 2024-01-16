using Mediathek.Extensions;
using Mediathek.Services.MediaLibrary.Scanner.FTP;
using Mediathek.Services.MediaLibrary.Scanner.Models;
using Mediathek.Services.MediaLibrary.Scanner.Shares;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mediathek.Services.MediaLibrary.Scanner.SSH
{
    internal class SSHScanner: RemoteSourceScanner
    {

        private SSHShare share = null;

        public override bool CanScan(MediaElementSource source)
        {
            return source is SSHMediaSource;
        }

        public override void DownloadFile(string sourceFilePath, string destFilePath)
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

        public override void Scan(MediaElementSource source, bool noContinue)
        {
            throw new NotImplementedException();
        }

        public override void Scan(MediaElementSource source, MediaItem mediaItem)
        {
            throw new NotImplementedException();
        }

        public override bool TestConnection(MediaElementSource mediaSource)
        {
            HttpClient client = new HttpClient();
            string response = client.GetStringAsync("http://mstromberg.ddns.net:50010/Folder?path=%2FMediaServer/Disk2/Serien/Das%20Leben%20und%20ich")
                                    .Wait<string>();

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

        public override void DownloadThumbnail(
            string originalSourceFilePath,
            string destFilePath,
            int maxWidth,
            int maxHeight)
        {
            throw new NotImplementedException();
        }

    }
}
