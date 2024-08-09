using System.Text.Json.Nodes;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Library.SourceReader
{
    [ServiceModelReference(typeof(HttpMediaSource))]
    public class HttpSourceReader: SourceReader
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
            return new SourceFolder() { FullPath = HttpMediaSource.Uri, Name = HttpMediaSource.Name, };
        }

        private string lastRequestUri = string.Empty;
        private DateTime lastRequestTime = DateTime.MinValue;
        private string lastRequestResponse = string.Empty;

        private async Task<string> Request(string fullPath)
        {
            if ((lastRequestUri == fullPath) && (lastRequestTime.AddSeconds(30) > DateTime.Now))
                return lastRequestResponse;
            lastRequestUri = string.Empty;
            HttpClient client = new HttpClient() { BaseAddress = new Uri(fullPath) };
            client.DefaultRequestHeaders.Add("X-ApiKey", "e568205d-f5ae-4754-954f-c0f56a266078");
            lastRequestResponse = await client.GetStringAsync(fullPath);
            lastRequestTime = DateTime.Now;
            lastRequestUri = fullPath;
            return lastRequestResponse;
        }

        private IEnumerable<SourceFile> ParseFiles(SourceFolder parentFolder, string json)
        {
            var node = JsonObject.Parse(json).AsObject();
            var files = node["files"].AsArray();
            return files.Select(f =>
                                new SourceFile()
                {
                    FullPath = Path.Combine(parentFolder.FullPath, f["name"].AsValue().ToString()),
                    Name = f["name"].AsValue().ToString(),
                    LastWriteTime = DateTime.Parse(f["lastWriteTime"].AsValue().ToString())
                });
        }

        private IEnumerable<SourceFolder> ParseFolders(SourceFolder parentFolder, string json)
        {
            var node = JsonObject.Parse(json).AsObject();
            var files = node["directories"].AsArray();
            return files.Select(f =>
                                new SourceFolder()
                {
                    FullPath = Path.Combine(parentFolder.FullPath, f["name"].AsValue().ToString()),
                    Name = f["name"].AsValue().ToString(),
                    LastWriteTime = DateTime.Parse(f["lastWriteTime"].AsValue().ToString())
                });
        }

        public override async Task<IEnumerable<SourceFile>> ReadFilesAsync(SourceFolder folder)
        {
            var json = await Request(folder.FullPath);
            return ParseFiles(folder, json);
        }

        public override async Task<IEnumerable<SourceFolder>> ReadFoldersAsync(SourceFolder folder)
        {
            var json = await Request(folder.FullPath);

            return ParseFolders(folder, json);
        }

    }
}
