using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(Movie))]
    public class MovieMediaListItem : BaseMediaListItem
    {
        public MovieMediaListItem(ClassifiedEntry item) 
            : base(item)
        {
            var movie = ((Movie)item);
            if (!string.IsNullOrWhiteSpace(movie.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, movie.PicturePath));
        }

        
    }
}
