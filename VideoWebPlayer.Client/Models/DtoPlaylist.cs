namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Represents a playlist.
    /// </summary>
    public class DtoPlaylist
    {
        /// <summary>
        /// Gets or sets the id of the playlist.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the playlist.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the playlist.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the sort mode of the playlist (see <see cref="PlaylistSortModeValues"/>).
        /// </summary>
        public string SortMode { get; set; } = PlaylistSortModeValues.ByReleaseDate;

        /// <summary>
        /// Gets or sets the creation timestamp of the playlist.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp the playlist was last updated at.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the playlist's genres to display (in the overview and detail view, and usable for
        /// filtering/searching just like the genres of other content), ordered by frequency descending
        /// (how many distinct titles in the playlist carry each genre) and capped to a small fixed number
        /// of entries so the display stays readable. By default automatically derived from the genres of
        /// the titles the playlist contains, recomputed whenever those change - unless
        /// <see cref="GenresManuallyOverridden"/> is set, in which case this reflects the owner's manual
        /// selection instead (also capped the same way, ordered by name since a manual pick has no
        /// frequency signal). See <see cref="AllGenreIds"/> for the full, uncapped set.
        /// </summary>
        public DtoGenreOption[] Genres { get; set; } =
            Array.Empty<DtoGenreOption>();

        /// <summary>
        /// Gets or sets every genre id currently assigned to the playlist (derived or manually
        /// overridden), not just the capped, frequency-sorted subset in <see cref="Genres"/>. Used to
        /// prefill the manual-override editor with the playlist's actual current selection.
        /// </summary>
        public long[] AllGenreIds { get; set; } =
            Array.Empty<long>();

        /// <summary>
        /// Gets or sets a value indicating whether the owner has manually overridden the playlist's
        /// genres. While <see langword="true"/>, <see cref="Genres"/>/<see cref="AllGenreIds"/> reflect the
        /// owner's manual selection and are no longer recomputed automatically when the playlist's
        /// contents change.
        /// </summary>
        public bool GenresManuallyOverridden { get; set; }
    }
}
