using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(Movie))]
    public class MovieMediaListItem : BaseMediaListItem
    {
        public MovieMediaListItem(ClassifiedEntry item, IResourceManager resourceManager, ILogger logger) 
            : base(item, resourceManager, logger)
        {
        }

        protected Movie Movie => base.Item as Movie;
        protected override void UpdatePicture(IPicturedEntry item)
        {
            switch (ApplicationArea)
            {
                case CardItemApplicationArea.Single:
                    if (Movie is not null)
                        base.UpdatePicture(Movie);
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
                    if (Movie is not null)
                    {
                        Title = Movie.Name;
                        Subtitle = GetDateTimeInfo(Movie.ReleaseDate, Movie.PremieredAt);
                    }
                    if (string.IsNullOrWhiteSpace(Title))
                        base.UpdateMediaInformation(Movie);
                    break;
                default:
                    base.UpdateMediaInformation(Movie);
                    break;
            }
        }

    }
}
