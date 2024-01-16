using Mediathek.Services.Database;
using System;
using System.Linq;

namespace Mediathek.Models.TVShows
{
    [DataModelReference(typeof(Services.Database.Models.TVShow))]
    public class TVShow: BaseModel
    {

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
        public ImageSource Picture { get; set; }

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

        [Path(nameof(BannerPath))]
        public ImageSource Banner { get; set; }

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

        public override string ToString()
        {
            return $"{Name}";
        }

    }
}
