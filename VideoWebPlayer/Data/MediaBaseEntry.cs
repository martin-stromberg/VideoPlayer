using System;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Basisklasse für alle Medieneinträge (z.B. Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection).
    /// </summary>
    public abstract class MediaBaseEntry
    {
        /// <summary>
        /// Gets or sets the entry identifier.
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the release date.
        /// </summary>
        public DateTime? ReleaseDate { get; set; }
        /// <summary>
        /// Gets or sets the premiered date.
        /// </summary>
        public DateTime? PremieredAt { get; set; }
        /// <summary>
        /// Gets or sets the end date.
        /// </summary>
        public DateTime? EndedAt { get; set; }

        /// <summary>
        /// Gets or sets the media source identifier.
        /// </summary>
        public long MediaSourceId { get; set; }
        /// <summary>
        /// Gets or sets the collection identifier.
        /// </summary>
        public long CollectionId { get; set; }

        /// <summary>
        /// Gets or sets the created timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets the last classified timestamp.
        /// </summary>
        public DateTime? ClassifiedAt { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the entry has changed.
        /// </summary>
        public bool Changed { get; set; }

        /// <summary>
        /// Gets or sets the poster picture identifier.
        /// </summary>
        public long? PosterPictureId { get; set; }
        /// <summary>
        /// Gets or sets the banner picture identifier.
        /// </summary>
        public long? BannerPictureId { get; set; }
        /// <summary>
        /// Gets or sets the fanart picture identifier.
        /// </summary>
        public long? FanartPictureId { get; set; }
        /// <summary>
        /// Gets or sets the poster picture.
        /// </summary>
        public Picture? PosterPicture { get; set; }
        /// <summary>
        /// Gets or sets the banner picture.
        /// </summary>
        public Picture? BannerPicture { get; set; }
        /// <summary>
        /// Gets or sets the fanart picture.
        /// </summary>
        public Picture? FanartPicture { get; set; }

    }
}