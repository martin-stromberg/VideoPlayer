using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Library.SourceReader
{
    public class SourceReaderRequestEventArgs: SourceReaderEventArgs
    {

        public SourceReaderRequestEventArgs(MediaSource mediaSource)
        {
            MediaSource = mediaSource;
        }

        public MediaSource MediaSource { get; }

    }
}
