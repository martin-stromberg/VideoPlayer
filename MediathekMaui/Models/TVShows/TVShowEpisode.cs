using Mediathek.Services.Database;
using System;
using System.Linq;

namespace Mediathek.Models.TVShows
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

        [Path(nameof(PicturePath))]
        public ImageSource Picture { get; set; }

        public long SeasonId { get; set; }

        public string SeasonName { get; set; }

        public string EpisodeNo { get; set; }

        public string Part { get; set; }

        public string Plot { get; set; }

        public DateTime AiredAt { get; set; }

        public long[] MediaItems { get; set; }

        [FieldModelReference(nameof(Id), nameof(Services.Database.Models.TVShowEpisode.PrimaryMediaItemId))]
        public MediaItem PrimaryMediaItem { get; set; }

        [FieldModelReference(nameof(Id), nameof(Services.Database.Models.TVShowEpisode.DownloadMediaItemId))]
        public MediaItem DownloadMediaItem { get; set; }

        internal TVShowEpisode SetMediaItems(IEnumerable<Services.Database.Models.TVShowEpisodeMediaItem> mediaItems)
        {
            MediaItems = mediaItems.Select(mi => mi.MediaItemId).ToArray();
            return this;
        }

    }
}
