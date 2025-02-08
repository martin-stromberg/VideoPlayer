using Microsoft.Extensions.Logging;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Sources;
using VideoPlayer.Service.Library.SourceReader;

namespace VideoPlayer.Service.Library.Scanner.Classification
{
    public abstract class BaseClassifier: BaseService
    {

        public BaseClassifier(
            IMediaLibrary mediaLibrary,
            ILogger logger)
            :base(logger)
        {
            MediaLibrary = mediaLibrary;
        }

        public abstract bool Classify(MediaItem mediaItem);
        public abstract bool UpdatePictures(MediaItem mediaItem);
        public abstract Task DeleteOrpahnedPictures(Action callback);
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

        public abstract bool UpdatePictures(Actor actor);

        public abstract Task RecaptureInvalidPictures(Action value);
    }

}
