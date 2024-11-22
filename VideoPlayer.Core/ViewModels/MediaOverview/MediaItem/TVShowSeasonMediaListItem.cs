using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(TVShowSeason))]
    public class TVShowSeasonMediaListItem: BaseMediaListItem
    {
        public TVShowSeasonMediaListItem(ClassifiedEntry item)
            : base(item)
        {
            var season = ((TVShowSeason)item);
            if (!string.IsNullOrWhiteSpace(season.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, season.PicturePath));
        }
        protected TVShowSeason Season => base.Item as TVShowSeason;
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
