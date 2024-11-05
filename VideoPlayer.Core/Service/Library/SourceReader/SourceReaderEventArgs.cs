using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Library.SourceReader
{
    public class SourceReaderEventArgs: EventArgs
    {

        public ISourceReader Reader { get; set; }

        public MediaSource Source { get; set; }

    }
}
