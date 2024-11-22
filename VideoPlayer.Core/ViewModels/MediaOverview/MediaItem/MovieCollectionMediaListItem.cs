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
            IsCollection = true;
        }
        protected MovieCollection Collection => base.Item as MovieCollection;
        protected override void UpdateMediaInformation(ClassifiedEntry item)
        {
            switch (ApplicationArea)
            {
                case CardItemApplicationArea.Single:
                    if (Collection is not null)
                    {
                        Title = Collection.Name;
                        Subtitle = GetDateTimeInfo(Collection.ReleaseDate, Collection.PremieredAt);
                    }
                    if (string.IsNullOrWhiteSpace(Title))
                        base.UpdateMediaInformation(Collection);
                    break;
                default:
                    base.UpdateMediaInformation(Collection);
                    break;
            }
        }

    }
}
