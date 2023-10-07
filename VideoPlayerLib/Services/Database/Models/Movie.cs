using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayerLib.Services.Database.Models
{
    public class Movie: BaseDataModel
    {
        public string Genre { get; set; }
        public string Plot { get; set; }
        public string PicturePath { get; set; }
        public long CollectionId { get; set; }
    }
}
