using System;
using System.Linq;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{

    [DataModelReference(typeof(Database.Models.Movie))]
    public class Movie : BaseModel
    {
        public string Genre { get; set; }
        public string Plot { get; set; }

        public long[] MediaItems { get; set; }
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

        public long CollectionId
        {
            get { return GetProperty<long>(); }
            set { SetProperty<long>(value); }
        }

        internal Movie SetMediaItems(IEnumerable<Database.Models.MovieMediaItem> mediaItems)
        {
            MediaItems = mediaItems.Select(mi => mi.MediaItemId).ToArray();
            return this;
        }

    }
}
