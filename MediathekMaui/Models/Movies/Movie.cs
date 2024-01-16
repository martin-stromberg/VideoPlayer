using Mediathek.Services.Database;
using System;
using System.Linq;

namespace Mediathek.Models.Movies
{

    [DataModelReference(typeof(Services.Database.Models.Movie))]
    public class Movie: BaseModel
    {

        public string Genre
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

        public string Plot
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

        public DateTime Date
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

        public string Genres
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

        public string Language
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

        public DateTime PremieredAt
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

        public string Countries
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

        public bool IsSingleCollectionMovie
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

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

        [Path(nameof(PicturePath))]
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

        [FieldModelReference(nameof(Id), nameof(Services.Database.Models.Movie.TrailerMediaItemId))]
        public MediaItem TrailerMediaItem { get; set; }

        internal Movie SetMediaItems(IEnumerable<Services.Database.Models.MovieMediaItem> mediaItems)
        {
            MediaItems = mediaItems.Select(mi => mi.MediaItemId).ToArray();
            return this;
        }

        public override string ToString()
        {
            return $"{Name}";
        }

    }
}
