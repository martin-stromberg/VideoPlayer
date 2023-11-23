using System;
using System.Linq;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.TVShows
{
    [DataModelReference(typeof(Services.Database.Models.TVShowSeason))]
    public class TVShowSeason: BaseModel
    {

        public long ShowId { get; set; }

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

        public ImageSource Banner { get; set; }

        public override string ToString()
        {
            return Name;
        }

    }
}
