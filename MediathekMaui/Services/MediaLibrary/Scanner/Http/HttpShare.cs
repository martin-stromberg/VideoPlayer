using Mediathek.Extensions;
using Mediathek.Services.MediaLibrary.Scanner.Shares;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;

namespace Mediathek.Services.MediaLibrary.Scanner.Http
{
    public class HttpShare: RemoteShare
    {

        private readonly string serverUri;
        private readonly string apiKey = "e568205d-f5ae-4754-954f-c0f56a266078";

        public HttpShare(string serverUri)
        {
            this.serverUri = serverUri;
        }

        private string Get(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-ApiKey", apiKey);
                return client.GetStringAsync(url).Wait<string>();
            }
        }

        private JObject GetJson(string uri)
        {
            try
            {
                return JsonConvert.DeserializeObject(Get(uri)) as JObject;
            }
            catch (Exception ex)
            { 
                Debug.WriteLine(ex);
                return null; 
            }
        }

        public IEnumerable<HttpFileInfo> ListFiles(string path)
        {
            var uri = $"{serverUri}Folder?path={path}";
            var response = GetJson(uri);
            return GetFiles(response, path);
        }

        private IEnumerable<HttpFileInfo> GetFiles(JObject response, string path)
        {
            JArray collection = null;
            try
            {
                collection = response["files"] as JArray;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"{ex.Message}");
            }
            if (collection is not null)
                foreach (var entry in collection)
                {
                    if (!DateTime.TryParse($"{entry["lastWriteTime"]}", out DateTime lastWriteTime))
                        lastWriteTime = DateTime.MinValue;
                    yield return new HttpFileInfo()
                    {
                        Name = $"{entry["name"]}",
                        Path = $"{path}{entry["name"]}",
                        LastWriteTime = lastWriteTime
                    };
                }
        }

        private IEnumerable<HttpFileInfo> GetFolders(JObject response, string path)
        {
            JArray collection = null;
            try
            {
                collection = response["directories"] as JArray;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex.Message}");
            }
            if (collection != null)
                foreach (var entry in collection)
                {
                    if (!DateTime.TryParse($"{entry["lastWriteTime"]}", out DateTime lastWriteTime))
                        lastWriteTime = DateTime.MinValue;
                    yield return new HttpFileInfo()
                    {
                        Name = $"{entry["name"]}",
                        Path = $"{path}/{entry["name"]}",
                        LastWriteTime = lastWriteTime
                    };
                }
        }

        public IEnumerable<HttpFileInfo> ListDirectories(string path)
        {
            var uri = $"{serverUri}Folder?path={path}";
            var response = GetJson(uri);
            return GetFolders(response, path);
        }

        public override void DownloadFile(string remoteFilePath, string localFilePath)
        {
            remoteFilePath = remoteFilePath.Replace('\\', '/');
            var localFolderPath = Path.GetDirectoryName(localFilePath);
            if (!Path.Exists(localFolderPath))
                Directory.CreateDirectory(localFolderPath);
            var tempFilePath = $"{localFilePath}.temp";
            try
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
                if (File.Exists(localFolderPath))
                    File.Delete(localFilePath);
                using (HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMinutes(60) })
                {
                    client.DefaultRequestHeaders.Add("X-ApiKey", apiKey);
                    var uri = $"{serverUri}File?path={remoteFilePath}";
                    using (var fs = new FileStream(tempFilePath, FileMode.CreateNew))
                    {
                        CancellationTokenSource cancelationToken = new CancellationTokenSource();
                        DownloadProgressEventArgs progressArgs = new DownloadProgressEventArgs(remoteFilePath,
                                                                                               localFilePath,
                                                                                               0);
                        client.DownloadAsync(uri,
                                             fs,
                                             new Progress<float>((progress) =>
                        {
                            progressArgs.Progress = progress;
                            OnDownloadProgress(progressArgs);
                            if (progressArgs.Cancel)
                                cancelationToken.Cancel();
                        }),
                                             cancelationToken.Token)
                              .Wait();
                    }
                    File.Move(tempFilePath, localFilePath);
                }
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        public void DownloadThumbnail(string remoteFilePath, string localFilePath, int maxWidth, int maxHeight)
        {
            remoteFilePath = remoteFilePath.Replace('\\', '/');
            var localFolderPath = Path.GetDirectoryName(localFilePath);
            if (!Path.Exists(localFolderPath))
                Directory.CreateDirectory(localFolderPath);
            using (HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMinutes(60) })
            {
                client.DefaultRequestHeaders.Add("X-ApiKey", apiKey);
                var uri = $"{serverUri}Thumbnail?imagepath={remoteFilePath}&maxWidth={maxWidth}&maxHeight={maxHeight}";
                using (var fs = new FileStream(localFilePath, FileMode.CreateNew))
                {
                    CancellationTokenSource cancelationToken = new CancellationTokenSource();
                    DownloadProgressEventArgs progressArgs = new DownloadProgressEventArgs(remoteFilePath,
                                                                                           localFilePath,
                                                                                           0);
                    client.DownloadAsync(uri,
                                         fs,
                                         new Progress<float>((progress) =>
                    {
                        progressArgs.Progress = progress;
                        OnDownloadProgress(progressArgs);
                        if (progressArgs.Cancel)
                            cancelationToken.Cancel();
                    }),
                                         cancelationToken.Token)
                          .Wait();
                }
            }
        }

        internal void UploadFile(string remoteFilePath, string localFilePath, bool isTextFile)
        {
            remoteFilePath = remoteFilePath.Replace('\\', '/');
            using (HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMinutes(60) })
            {
                client.DefaultRequestHeaders.Add("X-ApiKey", apiKey);
                var uri = $"{serverUri}File?path={remoteFilePath}&overwrite=true&isTextFile={(isTextFile ? "true" : "false")}";
                var formContent = new MultipartFormDataContent();
                var fileData = File.ReadAllBytes(localFilePath);
                var fileContent = new ByteArrayContent(fileData);

                formContent.Add(fileContent, "file", Path.GetFileName(localFilePath));
                HttpResponseMessage response = client.PostAsync(uri, formContent).Wait<HttpResponseMessage>();
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new ApplicationException($"{response.StatusCode}: {response.Content.ReadAsStringAsync().Wait<string>()}");
            }
        }

        internal void TestConnection()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-ApiKey", apiKey);
                var uri = $"{serverUri}Folder?path=/";
                var responseContent = client.GetStringAsync(uri).Wait<string>();
            }
        }

    }
}

