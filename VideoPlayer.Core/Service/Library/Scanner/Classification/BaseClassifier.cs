using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.SourceReader;

namespace VideoPlayer.Service.Library.Scanner.Classification
{
    public abstract class BaseClassifier: BaseService
    {

        public BaseClassifier(IMediaLibrary mediaLibrary)
        {
            MediaLibrary = mediaLibrary;
        }

        public abstract Task<bool> Classify(MediaItem mediaItem);
        public abstract Task<bool> UpdatePictures(MediaItem mediaItem);

        public IMediaLibrary MediaLibrary { get; private set; }

        public event EventHandler<SourceReaderRequestEventArgs> SourceReaderRequest;

        private void OnSourceReaderRequest(SourceReaderRequestEventArgs e)
        {
            SourceReaderRequest?.Invoke(this, e);
        }

        protected ISourceReader CreateReader(MediaSource mediaSource)
        {
            var e = new SourceReaderRequestEventArgs(mediaSource);
            OnSourceReaderRequest(e);
            return e.Reader;
        }
    }

}
