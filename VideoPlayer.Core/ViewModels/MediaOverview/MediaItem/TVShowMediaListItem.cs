using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(TVShow))]
    public class TVShowMediaListItem : BaseMediaListItem
    {
        public TVShowMediaListItem(ClassifiedEntry item)
            : base(item)
        {
            var show = ((TVShow)item);
            if (!string.IsNullOrWhiteSpace(show.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, show.PicturePath));
        }


    }
}
