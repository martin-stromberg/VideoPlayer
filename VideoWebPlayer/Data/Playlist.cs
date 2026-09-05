using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a user-owned playlist.
    /// </summary>
    public class Playlist
    {
        /// <summary>
        /// Gets or sets the playlist identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the owning user identifier.
        /// </summary>
        [Required]
        [ForeignKey(nameof(ApplicationUser))]
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the playlist name.
        /// </summary>
        [Required]
        [MaxLength(255)]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the optional playlist description.
        /// </summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the sort mode used for the playlist contents.
        /// </summary>
        public PlaylistSortMode SortMode { get; set; } = PlaylistSortMode.ByReleaseDate;

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the last update timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
