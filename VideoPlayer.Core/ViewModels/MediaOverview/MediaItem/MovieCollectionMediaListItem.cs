using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(MovieCollection))]
    public class MovieCollectionMediaListItem : BaseMediaListItem
    {
        public MovieCollectionMediaListItem(ClassifiedEntry item)
            : base(item)
        {
            var movie = ((MovieCollection)item);
            if (!string.IsNullOrWhiteSpace(movie.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, movie.PicturePath));
        }


    }
}
