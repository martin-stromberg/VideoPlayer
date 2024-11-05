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
    [ServiceModelReference(typeof(TVShowEpisode))]
    public class TVShowEpisodeMediaListItem: BaseMediaListItem
    {
        public TVShowEpisodeMediaListItem(ClassifiedEntry entry)
            : base(entry) 
        {
            var episode = ((TVShowEpisode)entry);
            if (!string.IsNullOrWhiteSpace(episode.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, episode.PicturePath));
            else if(!string.IsNullOrWhiteSpace(episode.BannerPath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, episode.BannerPath));
        }
    }
}
