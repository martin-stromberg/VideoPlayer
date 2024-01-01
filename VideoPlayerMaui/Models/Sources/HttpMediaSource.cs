using Newtonsoft.Json;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Services.Database;
using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Models.Sources
{
    [DataModelReference(
       typeof(Services.Database.Models.MediaSource),
       FilterPropertyName = nameof(Type),
       FilterPropertyValue = nameof(HttpMediaSource))]
    public class HttpMediaSource: RemoteMediaSource
    {
        public HttpMediaSource()
            : base()
        {
            Type = nameof(HttpMediaSource);
            PathDelimiter = '/';
        }

        public string Uri {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        protected override void UpdateFromDataModel(BaseDataModel dataModel)
        {
            BeginUpdate();
            try
            {
                base.UpdateFromDataModel(dataModel);
                try
                {
                    var obj = JsonConvert.DeserializeObject<HttpMediaSource>(Configuration);
                    Uri = obj.Uri;
                    Path = obj.Path;
                }
                catch { }
            }
            finally
            {
                EndUpdate();
            }
        }

        public override string GetItemPath(MediaItems.MediaItem item)
        {
            switch (item.CopyType)
            {
                case MediaItemCopyType.Cache:
                    return item.Path;
                default:
                    return $"{Uri}{item.Path}";
            }
        }

        public override void Update(MediaSource newSource)
        {
            base.Update(newSource);
            Uri = ((HttpMediaSource)newSource).Uri;
        }
    }
}
