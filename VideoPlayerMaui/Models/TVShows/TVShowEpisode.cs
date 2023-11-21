using System;
using System.Linq;
using VideoPlayer.Services.Database;
using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Models.TVShows
{
    [DataModelReference(typeof(Services.Database.Models.TVShowEpisode))]
    public class TVShowEpisode: BaseModel
    {

        public string ShowName { get; set; }

        public string PicturePath
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
                if (value == null)
                    Picture = null;
                else
                    Picture = ImageSource.FromFile(value);
            }
        }

        public ImageSource Picture { get; set; }

        public long SeasonId { get; set; }

        public string SeasonName { get; set; }

        public string EpisodeNo { get; set; }

        public long[] MediaItems { get; set; }

        [FieldModelReference(nameof(Models.MediaItems.MediaItem.Id), nameof(Services.Database.Models.TVShowEpisode.PrimaryMediaItemId))]
        public MediaItems.MediaItem PrimaryMediaItem { get; set; }

        [FieldModelReference(nameof(Models.MediaItems.MediaItem.Id), nameof(Services.Database.Models.TVShowEpisode.DownloadMediaItemId))]
        public MediaItems.MediaItem DownloadMediaItem { get; set; }

        internal TVShowEpisode SetMediaItems(IEnumerable<TVShowEpisodeMediaItem> mediaItems)
        {
            MediaItems = mediaItems.Select(mi => mi.MediaItemId).ToArray();
            return this;
        }

    }
}
