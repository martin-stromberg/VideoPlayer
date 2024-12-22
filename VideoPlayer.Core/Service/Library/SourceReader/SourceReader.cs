using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Sources;

namespace VideoPlayer.Service.Library.SourceReader
{
    public abstract class SourceReader: ISourceReader
    {

        public SourceReader(MediaSource mediaSource)
        {
            MediaSource = mediaSource;
        }

        public MediaSource MediaSource { get; private set; }

        public abstract SourceFolder GetRoot();

        public abstract IEnumerable<SourceFolder> ReadFolders(SourceFolder folder);

        public abstract IEnumerable<SourceFile> ReadFiles(SourceFolder folder);
        public abstract SourceFile ReadFile(MediaItem mediaItem);
        public abstract FileInfo Download(MediaItem nfoFile, Action<decimal> progressCallback);

        public abstract string ReadTextFile(MediaItem nfoFile);
    }
}
