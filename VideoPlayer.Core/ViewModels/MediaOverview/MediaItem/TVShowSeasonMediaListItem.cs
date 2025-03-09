using Microsoft.Extensions.Logging;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(TVShowSeason))]
    public class TVShowSeasonMediaListItem: BaseMediaListItem
    {
        public TVShowSeasonMediaListItem(ClassifiedEntry item, IResourceManager resourceManager, ILogger logger)
            : base(item, resourceManager, logger)
        {
        }
        protected TVShowSeason Season => base.Item as TVShowSeason;
        protected override void UpdatePicture(IPicturedEntry item)
        {
            switch (ApplicationArea)
            {
                case CardItemApplicationArea.Single:
                    if (Season is not null)
                        base.UpdatePicture(Season);
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
                    if (Season is not null)
                    {
                        Title = Season.ShowName;
                        Subtitle = $"Staffel {Season.Number}";
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
