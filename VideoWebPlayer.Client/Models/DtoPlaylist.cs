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
    }
}
