using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(MediaDataSource))]
    public class HttpMediaSource: MediaSource
    {

        public HttpMediaSource()
            : this(null) { }

        public HttpMediaSource(MediaDataSource dataModel)
            : base(dataModel)
        {
            if (dataModel is not null)
                if (dataModel.Type != MediaSourceType.Http)
                    throw new ArgumentException(nameof(MediaDataSource.Type));
        }

        public string Uri
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty(value);
            }
        }

    }
}
