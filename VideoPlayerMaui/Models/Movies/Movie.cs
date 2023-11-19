using System;
using System.Linq;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.Movies
{

    [DataModelReference(typeof(Services.Database.Models.Movie))]
    public class Movie: BaseModel
    {

        public string Genre { get; set; }

        public string Plot { get; set; }

        public DateTime Date { get; set; }

        public long[] MediaItems { get; set; }

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

        public long CollectionId
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

        internal Movie SetMediaItems(IEnumerable<Services.Database.Models.MovieMediaItem> mediaItems)
        {
            MediaItems = mediaItems.Select(mi => mi.MediaItemId).ToArray();
            return this;
        }

    }
}
