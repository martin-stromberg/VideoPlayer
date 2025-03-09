using Microsoft.Extensions.Logging;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(TVShow))]
    public class TVShowMediaListItem : BaseMediaListItem
    {
        public TVShowMediaListItem(ClassifiedEntry item, IResourceManager resourceManager, ILogger logger)
            : base(item, resourceManager, logger)
        {
        }
        protected TVShow Show => base.Item as TVShow;
        protected override void UpdatePicture(IPicturedEntry item)
        {
            switch (ApplicationArea)
            {
                case CardItemApplicationArea.Single:
                    if (Show is not null)
                        base.UpdatePicture(Show);
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
            switch(ApplicationArea)
            {
                case CardItemApplicationArea.Single:
                    if (Show is not null)
                    {
                        Title = Show.Name;
                        Subtitle = GetDateTimeInfo(item.ReleaseDate, item.PremieredAt);
                    }                    
                    if (string.IsNullOrWhiteSpace(Title))
                        base.UpdateMediaInformation(item);
                    break;
                default:
                    base.UpdateMediaInformation(item);
                    break;
            }            
        }

        
    }
}
