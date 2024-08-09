using VideoPlayer.Service.Library.Models;

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

        public abstract Task<IEnumerable<SourceFolder>> ReadFoldersAsync(SourceFolder folder);

        public abstract Task<IEnumerable<SourceFile>> ReadFilesAsync(SourceFolder folder)


    }
}
