using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(MovieCollection))]
    public class MovieCollectionMediaListItem : BaseMediaListItem
    {
        public MovieCollectionMediaListItem(ClassifiedEntry item, IResourceManager resourceManager)
            : base(item, resourceManager)
        {
            IsCollection = true;
        }
        protected MovieCollection Collection => base.Item as MovieCollection;
        protected override void UpdatePicture(IPicturedEntry item)
        {
            switch (ApplicationArea)
            {
                case CardItemApplicationArea.Single:
                    if (Collection is not null)
                        base.UpdatePicture(Collection);
                    else
                        base.UpdatePicture(item);
                    break;
                default:
                    base.UpdatePicture(item);
                    break;
            }
        }
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
