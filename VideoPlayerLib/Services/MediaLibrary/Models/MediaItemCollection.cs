using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;
using VideoPlayerLib.Services.MediaLibrary.Models.Meta;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.MediaCollection))]
    public class MediaItemCollection: BaseModel
    {
        public long MediaSourceId
        {
            get { return GetProperty<long>(); }
            set { SetProperty<long>(value); }
        }
        public long ParentCollectionId
        {
            get { return GetProperty<long>(); }
            set { SetProperty<long>(value); }
        }
        public string Path
        {
            get { return GetProperty<string>(); }
            set { SetProperty<string>(value); }
        }

        public MediaInformation MetaInfo
        {
            get { return GetProperty<MediaInformation>(); }
            set { SetProperty<MediaInformation>(value); }
        }
        public string PicturePath
        {
            get { return GetProperty<string>(); }
            set
            {
                SetProperty<string>(value);
                if (value == null)
                    Picture = null;
                else
                    Picture = ImageSource.FromFile(value);
            }
        }
        public ImageSource Picture
        {
            get { return GetProperty<ImageSource>(); }
            set { SetProperty<ImageSource>(value); }
        }

        public DateTime MetaDataTime
        {
            get { return GetProperty<DateTime>(); }
            set { SetProperty<DateTime>(value); }
        }
    }
}
