using System;
using System.Linq;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.TVShows
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

        public ImageSource Picture { get; set; }

    }
}
