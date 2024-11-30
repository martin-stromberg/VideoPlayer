using VideoPlayer.Service.Library.Models.Sources;

namespace VideoPlayer.Service.Library.SourceReader
{
    public class SourceReaderEventArgs: EventArgs
    {

        public ISourceReader Reader { get; set; }

        public MediaSource Source { get; set; }

    }
}
