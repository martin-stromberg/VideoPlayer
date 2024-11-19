using SQLite;
using System;
using System.Linq;

namespace VideoPlayer.Service.Database.Models
{
    public enum DataEntryType
    {

        Movie = 0,
        TVShow = 10,
        TVShowSeason = 11,
        TVShowEpisode = 12,
        MovieCollection = 100,
        TVShowCollection = 110,

    }

    public class DataClassifiedEntry: BaseDataModel
    {

        public DataEntryType Type
        {
            get
            {
                return GetProperty<DataEntryType>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public bool Enabled
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
        public bool Visible 
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

        public string OriginalTitle
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

        public DateTime ReleaseDate
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

        public string PicturePath
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

        public string BannerPath
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

        [Indexed]
        public long MediaItemCollectionId
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

        [Indexed]
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

        public long TrailerMediaItemId
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

        public long DownloadMediaItemId
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

        public bool IsSingle
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

        public int Number {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(value);
            }
        }
        public string PartNo {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string BannerBackgroundColor {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }
        public string PictureBackgroundColor {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string ShowName {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public int SeasonNo {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(value);
            }
        }

        public DateTime LastWatched {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }
    }
}
