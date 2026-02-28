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
        /// Gets or sets the movie navigation property.
        /// </summary>
        [ForeignKey(nameof(MovieId))]
        public Movie? Movie { get; set; }

        /// <summary>
        /// Gets or sets the TV show episode navigation property.
        /// </summary>
        [ForeignKey(nameof(TVShowEpisodeId))]
        public TVShowEpisode? TVShowEpisode { get; set; }
    }
}