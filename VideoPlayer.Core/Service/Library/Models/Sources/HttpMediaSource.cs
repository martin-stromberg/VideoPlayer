using Newtonsoft.Json;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Sources
{
    [DataModelReference(typeof(MediaDataSource), ReferenceFieldName = nameof(MediaDataSource.Type), ReferenceFieldValue = nameof(MediaSourceType.Http))]
    public class HttpMediaSource : MediaSource
    {

        public HttpMediaSource()
            : this(null) { }

        public HttpMediaSource(MediaDataSource dataModel)
            : base(dataModel)
        {
            if (dataModel is not null)
            {
                if (dataModel.Type != MediaSourceType.Http)
                    throw new ArgumentException(nameof(MediaDataSource.Type));

                var copy = JsonConvert.DeserializeObject(dataModel.Configuration, typeof(HttpMediaSource)) as HttpMediaSource;
                Uri = copy.Uri;
            }
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

        protected override void AssignChanges()
        {
            base.AssignChanges();
            ((MediaDataSource)DataModel).Type = MediaSourceType.Http;
        }

    }
}
