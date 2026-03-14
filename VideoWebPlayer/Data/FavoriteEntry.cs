using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a user's favorite media entry.
    /// </summary>
    public class FavoriteEntry
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
        public required string UserId { get; set; } // Identity-User

        /// <summary>
        /// Gets or sets the movie collection identifier.
        /// </summary>
        public long? MovieCollectionId { get; set; }
        /// <summary>
        /// Gets or sets the TV show identifier.
        /// </summary>
        public long? TVShowId { get; set; }
        /// <summary>
        /// Gets or sets the TV show season identifier.
        /// </summary>
        public long? TVShowSeasonId { get; set; }
        /// <summary>
        /// Gets or sets the TV show episode identifier.
        /// </summary>
        public long? TVShowEpisodeId { get; set; }
        /// <summary>
        /// Gets or sets the movie identifier.
        /// </summary>
        public long? MovieId { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    }
}