using SMBLibrary;
using SmbTests.Samba;

namespace SmbTests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var serverName = "raspberrypi";
            var username = "mstro";
            var password = "Hi1TvM!nav";
            var shareNames = new string[] { "FileServer/Filme", "FileServer/Serien" };
            var doDownloadFile = false;
            var lookForNFO = false;
            var lookForFolderPicture = false;
            var lookForTVShow = false;
            var lookForPicFile = false;
            var filesToDownloads = new string[] { ".nfo" , ".jpg" };
            var folderPicName = "folder.jpg";
            var tvShowNFOName = "tvshow.nfo";

            Samba.SambaShare share = new Samba.SambaShare(serverName, username, password);
            Samba.SambaShareScanner scanner = new Samba.SambaShareScanner(share);
            scanner.FileFound += (s, e) =>
            {
                var ext = Path.GetExtension(e.File.Name);
                if (doDownloadFile && filesToDownloads.Contains(ext))
                {
                    FileInfo tempFile = new FileInfo(e.File.Name);
                    try
                    {
                        share.DownloadFile(e.File.Path, tempFile.FullName);
                    }
                    finally
                    {
                        tempFile.Refresh();
                        if (tempFile.Exists)
                            tempFile.Delete();

                    }
                }

                if (lookForNFO && ext != ".nfo")
                {
                    var folderPath = Path.GetDirectoryName(e.File.Path);
                    var nfoFileName = Path.ChangeExtension(e.File.Name, ".nfo");
                    var nfoFilePath = Path.ChangeExtension(e.File.Path, ".nfo");
                    var nfoFile = share.ListFiles(folderPath).FirstOrDefault(f => f.FileName == nfoFileName);
                    if (nfoFile != null)
                        DownloadFile(share, nfoFilePath);
                }
                if (lookForPicFile && !filesToDownloads.Contains(ext))
                {
                    var folderPath = Path.GetDirectoryName(e.File.Path);
                    var picFileName = Path.ChangeExtension(e.File.Name, ".jpg");
                    var picFilePath = Path.ChangeExtension(e.File.Path, ".jpg");
                    var picFile = share.ListFiles(folderPath).FirstOrDefault(f => f.FileName == picFileName);
                    if (picFile != null)
                        DownloadFile(share, picFilePath);
                }
            };
            scanner.FolderFound += (sender, e) =>
            {
                if (lookForFolderPicture)
                {
                    var pictureFilePath = Path.Combine(e.Folder.Path, folderPicName);
                    var pictureFile = share.ListFiles(e.Folder.Path).FirstOrDefault(f => f.FileName == folderPicName);
                    if (pictureFile != null)
                        DownloadFile(share, pictureFilePath);
                }
                if (lookForTVShow)
                {
                    var nfoFilePath = Path.Combine(e.Folder.Path, tvShowNFOName);
                    var nfoFile = share.ListFiles(e.Folder.Path).FirstOrDefault(f => f.FileName == tvShowNFOName);
                    if (nfoFile != null)
                        DownloadFile(share, nfoFilePath);
                }
            };

            //File Actions:
            doDownloadFile = false;
            lookForNFO = true;            
            lookForPicFile = true;
            //Folder Actions:
            lookForFolderPicture = true;
            lookForTVShow = true;
            //Start Scan
            foreach (var shareName in shareNames)
                scanner.Scan($"\\{shareName}");
        }

        private void DownloadFile(SambaShare share, string remoteFile)
        {
            FileInfo localFile = new FileInfo(Path.GetTempFileName());
            try
            {
                share.DownloadFile(remoteFile, localFile.FullName);
            }
            finally
            {
                localFile.Refresh();
                if (localFile.Exists)
                    localFile.Delete();
            }
        }
    }
}