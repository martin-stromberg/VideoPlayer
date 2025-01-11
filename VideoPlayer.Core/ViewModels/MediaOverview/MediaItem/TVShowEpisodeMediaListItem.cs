using System;
using VideoPlayer.Extensions;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tools;
using VideoPlayer.Service.Resources;
using Renci.SshNet;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    [ServiceModelReference(typeof(TVShowEpisode))]
    public class TVShowEpisodeMediaListItem: BaseMediaListItem
    {
        public TVShowEpisodeMediaListItem(ClassifiedEntry entry, IResourceManager resourceManager)
            : base(entry, resourceManager) 
        {
            var episode = ((TVShowEpisode)entry);
            if (!string.IsNullOrWhiteSpace(episode.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, episode.PicturePath));
            else if(!string.IsNullOrWhiteSpace(episode.BannerPath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, episode.BannerPath));
            else
                LoadDefaultImage();
        }

        public TVShowEpisodeMediaListItem(TVShowSeason season, ClassifiedEntry entry, IResourceManager resourceManager) 
            : this(entry, resourceManager)
        {
            var episode = ((TVShowEpisode)entry);
            if (season is not null)
                Title = $"S{season.Number}E{episode.Episode}: {episode.Name}";
        }

        protected TVShowEpisode Episode => base.Item as TVShowEpisode;
        protected override void UpdatePicture(IPicturedEntry item)
        {
            switch (ApplicationArea)
            {
                case CardItemApplicationArea.Single:
                    if (Episode is not null)
                        base.UpdatePicture(Episode);
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
                    if (Episode is not null)
                    {
                        Title = Episode.ShowName;
                        Subtitle = $"S{Episode.SeasonNo}E{Episode.Episode}: {Episode.Name}";
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
