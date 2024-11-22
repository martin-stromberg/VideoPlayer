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
        protected TVShow Show => base.Item as TVShow;
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
