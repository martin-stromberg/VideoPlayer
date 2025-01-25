using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Extensions;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using VideoPlayer.Service.Library.Models.Sources;

namespace VideoPlayer.Service.Library.SourceReader.Http
{

    [ServiceModelReference(typeof(HttpMediaSource))]
    public class HttpSourceReader : SourceReader
    {

        public HttpSourceReader(HttpMediaSource mediaSource)
            : base(mediaSource) { }

        public HttpMediaSource HttpMediaSource
        {
            get
            {
                return MediaSource as HttpMediaSource;
            }
        }

        public override SourceFolder GetRoot()
        {
            return new SourceFolder() { FullPath = HttpMediaSource.Uri, Path = "/", Name = HttpMediaSource.Name };
        }

        private RequestCache _Cache = new RequestCache();

        private string Request(string fullPath, bool skipCache = false)
        {
            if (!skipCache)
            {
                var response = _Cache.GetResponse(fullPath);
                if (!string.IsNullOrWhiteSpace(response))
                    return response;
            }

            HttpClient client = new HttpClient() { BaseAddress = new Uri(fullPath) };
            client.DefaultRequestHeaders.Add("X-ApiKey", "e568205d-f5ae-4754-954f-c0f56a266078");
            {
                var response = client.GetStringAsync(fullPath).Result;
                _Cache.Save(fullPath, response);
                return response;
            }
        }        

        private IEnumerable<SourceFile> ParseFiles(SourceFolder parentFolder, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new SourceFile[0];
            var node = JsonNode.Parse(json).AsObject();
            var files = node["files"].AsArray();
            return files.Select(f =>
            {
                var file = new SourceFile()
                {
                    FullPath = Path.Combine(parentFolder.FullPath, f["name"].AsValue().ToString()),
                    Name = f["name"].AsValue().ToString(),
                    LastWriteTime = DateTime.Parse(f["lastWriteTime"].AsValue().ToString())
                };
                file.Path = file.FullPath.Remove(0, HttpMediaSource.Uri.Length);
                return file;
            });
        }

        private IEnumerable<SourceFolder> ParseFolders(SourceFolder parentFolder, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new SourceFolder[0];
            var node = JsonNode.Parse(json).AsObject();
            var files = node["directories"].AsArray();
            return files.Select(f =>
            {
                var folder = new SourceFolder()
                {
                    FullPath = Path.Combine(parentFolder.FullPath, f["name"].AsValue().ToString()),
                    Name = f["name"].AsValue().ToString(),
                    LastWriteTime = DateTime.Parse(f["lastWriteTime"].AsValue().ToString())
                };
                folder.Path = folder.FullPath.Remove(0, HttpMediaSource.Uri.Length);
                return folder;
            });
        }

        public override IEnumerable<SourceFile> ReadFiles(SourceFolder folder)
        {
            var json = Request(folder.FullPath);
            return ParseFiles(folder, json);
        }

        public override IEnumerable<SourceFolder> ReadFolders(SourceFolder folder)
        {
            var json = Request(folder.FullPath);
            return ParseFolders(folder, json);
        }
        public override SourceFile ReadFile(MediaItem mediaItem)
        {
            var folderPath = Path.GetDirectoryName(mediaItem.Path);
            var folder = GetRoot();
            SourceFolder[] subFolders;
            while (folder is not null && folder.Path != folderPath)
            {
                subFolders = (ReadFolders(folder)).ToArray();
                folder = subFolders.FirstOrDefault(f =>
                    folderPath.StartsWith(f.Path)
                    && f.Path.Length <= folderPath.Length
                    && folderPath.Substring(0, f.Path.Length) == f.Path
                    && (folderPath.Remove(0, f.Path.Length) == ""
                    || folderPath.Remove(0, f.Path.Length).StartsWith("/"))
                    );
            }
            if (folder is not null)
                return (ReadFiles(folder)).FirstOrDefault(f => f.Name == mediaItem.Name);
            return null;
        }
        public override FileInfo Download(MediaItem file, Action<decimal> progressCallback)
        {
            string localFilePath = Path.GetTempFileName();
            File.Delete(localFilePath);
            var remoteFilePath = file.Path.Replace('\\', '/');
            var localFolderPath = Path.GetDirectoryName(localFilePath);
            if (!Path.Exists(localFolderPath))
                Directory.CreateDirectory(localFolderPath);
            //long fileSize = -1;
            //decimal currectProgress = 0;            
            using (HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMinutes(60) })
            {
                client.DefaultRequestHeaders.Add("X-ApiKey", "e568205d-f5ae-4754-954f-c0f56a266078");
                var uri = $"{HttpMediaSource.Uri}{file.Path}".Replace("Folder?", "File?");
                CancellationTokenSource cancelationToken = new CancellationTokenSource();

                using (var fileStream = new FileStream(localFilePath, FileMode.CreateNew))
                    try
                    {
                        client.DownloadAsync(uri, fileStream, new Progress<float>((progress) =>
                        {
                            progressCallback(Math.Round((decimal)progress * 100, 2));
                        }),
                                                 cancelationToken.Token).Wait();
                    }
                    catch (HttpClientRequestException)
                    {
                        File.Delete(localFilePath);
                    }
                    catch (Exception)
                    {
                        File.Delete(localFilePath);
                    }
            }
            return new FileInfo(localFilePath);
        }

        public override string ReadTextFile(MediaItem file)
        {
            var tempFile = Download(file, (p) => { });
            try
            {
                return File.ReadAllText(tempFile.FullName);
            }
            finally
            {
                tempFile.Refresh();
                if (tempFile.Exists)
                    tempFile.Delete();
            }
        }

        public override void Upload(string sourceFilePath, string destFilePath, Action<decimal> progressCallback)
        {
            throw new NotImplementedException();
        }
    }
}
