using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a single media entry contained in a <see cref="Data.Playlist"/>.
    /// </summary>
    public class PlaylistEntry
    {
        /// <summary>
        /// Gets or sets the entry identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the owning playlist identifier.
        /// </summary>
        [ForeignKey(nameof(Playlist))]
        public long PlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the owning playlist.
        /// </summary>
        public Playlist Playlist { get; set; } = null!;

        /// <summary>
        /// Gets or sets the media type (one of the values in <c>MediaTypeValues</c>).
        /// </summary>
        [Required]
        public required string MediaType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the referenced media content.
        /// </summary>
        public long MediaId { get; set; }

        /// <summary>
        /// Gets or sets the media type of the collection this entry was added through
        /// (e.g. <c>TVShow</c>, <c>TVShowSeason</c> or <c>MovieCollection</c>), or <c>null</c>
        /// for a top-level entry.
        /// </summary>
        public string? ParentMediaType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the collection this entry was added through, or
        /// <c>null</c> for a top-level entry.
        /// </summary>
        public long? ParentMediaId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the entry was added to the playlist.
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the manual sort order of the entry within its playlist, used when the owning
        /// playlist's <see cref="Data.PlaylistSortMode"/> is <see cref="PlaylistSortMode.Manual"/>.
        /// <c>null</c> for entries of a playlist sorted by <see cref="PlaylistSortMode.ByReleaseDate"/>.
        /// </summary>
        public long? SortOrder { get; set; }
    }
}
