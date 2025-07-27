using WebPlayerApi.Models;

namespace WebPlayerApi.Service.Data
{
    public abstract class SourceReader: ISourceReader
    {

        public SourceReader(MediaDirectory mediaSource)
        {
            MediaSource = mediaSource;
        }

        public MediaDirectory MediaSource { get; private set; }

        public abstract SourceFolder GetRoot();

        public abstract IEnumerable<SourceFolder> ReadFolders(SourceFolder folder);

        public abstract IEnumerable<SourceFile> ReadFiles(SourceFolder folder);
        public abstract SourceFile ReadFile(MediaItem mediaItem);
        public abstract FileInfo Download(MediaItem nfoFile, Action<decimal> progressCallback);

        public abstract string ReadTextFile(MediaItem nfoFile);

        public abstract void Upload(string sourceFilePath, string destFilePath, Action<decimal> progressCallback);
    }
}
