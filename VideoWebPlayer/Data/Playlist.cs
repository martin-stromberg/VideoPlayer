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
        /// The maximum allowed length of <see cref="Name"/>.
        /// </summary>
        public const int NameMaxLength = 255;

        /// <summary>
        /// The maximum allowed length of <see cref="Description"/>.
        /// </summary>
        public const int DescriptionMaxLength = 2000;

        /// <summary>
        /// The maximum number of genres displayed for a playlist (in the overview and detail view), even
        /// if more are derived from - or manually assigned to - its contents. Keeps the displayed genre
        /// list readable; every derived/assigned genre is still stored and still usable for filtering and
        /// searching (see <see cref="PlaylistGenre"/>), only the display is capped.
        /// </summary>
        public const int MaxDisplayedGenres = 5;

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
        [MaxLength(NameMaxLength)]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the optional playlist description.
        /// </summary>
        [MaxLength(DescriptionMaxLength)]
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

        /// <summary>
        /// Gets or sets a value indicating whether the owner has manually overridden the playlist's
        /// genres (<see cref="PlaylistGenre"/> rows for this playlist). While <see langword="true"/>, the
        /// genres are exactly what the owner picked and are no longer recomputed automatically when the
        /// playlist's contents change (<see cref="Services.PlaylistGenreService.RecomputeGenresAsync"/>
        /// becomes a no-op); resetting via <see cref="Services.PlaylistService.ResetPlaylistGenresAsync"/>
        /// clears this flag and immediately recomputes from the current contents again.
        /// </summary>
        public bool GenresManuallyOverridden { get; set; }

        /// <summary>
        /// Gets or sets the foreign key of the picture used as this playlist's cover (either uploaded by
        /// the owner or generated as a collage from the playlist's contents), or <see langword="null"/> if
        /// none has been set yet (the UI then falls back to a neutral placeholder).
        /// </summary>
        public long? CoverPictureId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="CoverPictureId"/> was uploaded by the owner
        /// (<see langword="true"/>) or automatically generated as a collage from the playlist's contents
        /// (<see langword="false"/>). Mirrors the "manually overridden" flag pattern of
        /// <see cref="GenresManuallyOverridden"/>: an uploaded cover always takes precedence and is never
        /// silently replaced by a generated one.
        /// </summary>
        public bool CoverPictureIsUserUploaded { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the picture referenced by <see cref="CoverPictureId"/>.
        /// </summary>
        public Picture? CoverPicture { get; set; }

        /// <summary>
        /// Gets or sets the entries contained in this playlist.
        /// </summary>
        public ICollection<PlaylistEntry> PlaylistEntries { get; set; } =
            new List<PlaylistEntry>();
    }
}
