using System;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Basisklasse für alle Medieneinträge (z.B. Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection).
    /// </summary>
    public abstract class MediaBaseEntry
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? ReleaseDate { get; set; }
        public DateTime? PremieredAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public long MediaSourceId { get; set; }
        public long CollectionId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ClassifiedAt { get; set; }
        public bool Changed { get; set; }

        public long? PosterPictureId { get; set; }
        public long? BannerPictureId { get; set; }
        public long? FanartPictureId { get; set; }
        public Picture? PosterPicture { get; set; }
        public Picture? BannerPicture { get; set; }
        public Picture? FanartPicture { get; set; }

    }
}