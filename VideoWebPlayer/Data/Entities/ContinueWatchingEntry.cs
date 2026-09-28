using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a persisted continue-watching entry for a user.
    /// </summary>
    public class ContinueWatchingEntry
    {
        /// <summary>
        /// Gets or sets the entry identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the user identifier.
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the movie identifier.
        /// </summary>
        public long? MovieId { get; set; }
        /// <summary>
        /// Gets or sets the TV show episode identifier.
        /// </summary>
        public long? TVShowEpisodeId { get; set; }

        /// <summary>
        /// Gets or sets the playlist identifier this entry is associated with, or <c>null</c> if the
        /// entry was created outside of a playlist playback context.
        /// </summary>
        public long? PlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the playback position.
        /// </summary>
        [Required]
        public TimeSpan Position { get; set; }

        /// <summary>
        /// Gets or sets the media duration.
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets the last updated timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the persisted user-specific list order.
        /// </summary>
        public long ListOrder { get; set; }

        /// <summary>
        /// Gets or sets the movie navigation property.
        /// </summary>
        [ForeignKey(nameof(MovieId))]
        public Movie? Movie { get; set; }

        /// <summary>
        /// Gets or sets the TV show episode navigation property.
        /// </summary>
        [ForeignKey(nameof(TVShowEpisodeId))]
        public TVShowEpisode? TVShowEpisode { get; set; }

        /// <summary>
        /// Gets or sets the playlist navigation property.
        /// </summary>
        [ForeignKey(nameof(PlaylistId))]
        public Playlist? Playlist { get; set; }
    }
}
