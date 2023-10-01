using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;
using VideoPlayerLib.Services.MediaLibrary.Models.Meta;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    public enum MediaItemCopyType { None, Cache }
    [DataModelReference(typeof(Database.Models.MediaItem))]
    public class MediaItem: BaseModel
    {
        public string Path
        {
            get { return GetProperty<string>(); }
            set { SetProperty<string>(value); }
        }
        public long ParentCollectionId
        {
            get { return GetProperty<long>(); }
            set { SetProperty<long>(value); }
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
        public long OriginalMediaItemId
        {
            get { return GetProperty<long>(); }
            set { SetProperty<long>(value); }
        }
        public MediaItemCopyType CopyType
        {
            get { return GetProperty<MediaItemCopyType>(); }
            set { SetProperty<MediaItemCopyType>(value); }
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
