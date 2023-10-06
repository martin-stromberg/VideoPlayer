using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.TVShowSeason))]
    public class TVShowSeason : BaseModel
    {
        public long ShowId { get; set; }
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
        public ImageSource Picture { get; set; }
    }
}
