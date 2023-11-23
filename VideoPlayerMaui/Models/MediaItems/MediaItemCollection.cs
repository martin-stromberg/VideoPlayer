using System;
using System.Linq;
using VideoPlayer.Models.MetaInformation;
using VideoPlayer.Services.Database;
using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Models.MediaItems
{
    [DataModelReference(typeof(MediaCollection))]
    public class MediaItemCollection: BaseModel
    {

        public long MediaSourceId
        {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public long ParentCollectionId
        {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public string Path
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public MediaInformation MetaInfo
        {
            get
            {
                return GetProperty<MediaInformation>();
            }
            set
            {
                SetProperty<MediaInformation>(value);
            }
        }

        public string PicturePath
        {
            get
            {
                return GetProperty<string>();
            }
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
            get
            {
                return GetProperty<ImageSource>();
            }
            set
            {
                SetProperty<ImageSource>(value);
            }
        }

        public string BannerPath
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
                if (value == null)
                    Banner = null;
                else
                    Banner = ImageSource.FromFile(value);
            }
        }

        public ImageSource Banner
        {
            get
            {
                return GetProperty<ImageSource>();
            }
            set
            {
                SetProperty<ImageSource>(value);
            }
        }

        public DateTime MetaDataTime
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        protected override void UpdateFromDataModel(BaseDataModel dataModel)
        {
            if (((MediaCollection)dataModel).MetaInfoJson == "null")
                ((MediaCollection)dataModel).MetaInfoJson = null;
            base.UpdateFromDataModel(dataModel);
        }

    }
}
