using System;
using System.Linq;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.TVShow))]
    public class TVShow : BaseModel
    {
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
