using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Syncfusion.XlsIO.Implementation.XmlSerialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Extensions;
using VideoPlayer.Services.MediaLibrary.Scanner.Models;
using VideoPlayer.Services.MediaLibrary.Scanner.Shares;

namespace VideoPlayer.Services.MediaLibrary.Scanner.Http
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
            return JsonConvert.DeserializeObject(Get(uri)) as JObject;
        }

        public IEnumerable<HttpFileInfo> ListFiles(string path)
        {
            var uri = $"{serverUri}Folder?path={path}";
            var response = GetJson(uri);
            return GetFiles(response, path);
        }

        private IEnumerable<HttpFileInfo> GetFiles(JObject response, string path)
        {
            var collection = response["files"] as JArray;
            if (collection != null)
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
            var collection = response["directories"] as JArray;
            if (collection != null)
                foreach (var entry in collection)
                    yield return new HttpFileInfo()
                    {
                        Name = $"{entry}",
                        Path = $"{path}{entry}",
                    };
        }

        public IEnumerable<HttpFileInfo> ListDirectories(string path)
        {
            var uri = $"{serverUri}Folder?path={path}";
            var response = GetJson(uri);
            return GetFolders(response, path);
        }

        public void DownloadFile(string remoteFilePath, string localFilePath)
        {
            remoteFilePath = remoteFilePath.Replace('\\', '/');
            var localFolderPath = Path.GetDirectoryName(localFilePath);
            using (HttpClient client = new HttpClient()
            {
                Timeout = TimeSpan.FromMinutes(60)
            })
            {
                client.DefaultRequestHeaders.Add("X-ApiKey", apiKey);
                var uri = $"{serverUri}File?path={remoteFilePath}";
                HttpResponseMessage response = client.GetAsync(uri).Wait<HttpResponseMessage>();
                System.Net.Http.HttpContent content = response.Content;
                using (var s = client.GetStreamAsync(uri).Wait<Stream>())
                {
                    if (!Directory.Exists(localFolderPath))
                        Directory.CreateDirectory(localFolderPath);
                    using (var fs = new FileStream(localFilePath, FileMode.CreateNew))
                    {
                        s.CopyToAsync(fs).Wait();
                    }
                }
            }
        }

        internal void UploadFile(string remoteFilePath, string localFilePath, bool isTextFile)
        {
            remoteFilePath = remoteFilePath.Replace('\\', '/');
            using (HttpClient client = new HttpClient()
            {
                Timeout = TimeSpan.FromMinutes(60)
            })
            {
                client.DefaultRequestHeaders.Add("X-ApiKey", apiKey);
                var uri = $"{serverUri}File?path={remoteFilePath}&overwrite=true&isTextFile={(isTextFile ? "true" : "false")}";
                var formContent = new MultipartFormDataContent();
                var fileData = File.ReadAllBytes(localFilePath);
                var fileContent = new ByteArrayContent(fileData);

                formContent.Add(fileContent, "file", Path.GetFileName(localFilePath));
                HttpResponseMessage response = client.PostAsync(uri, formContent).Wait<HttpResponseMessage>();
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new ApplicationException($"{response.StatusCode}: {response.Content.ReadAsStringAsync().Wait<string>()}");
            }
        }
    }
}


