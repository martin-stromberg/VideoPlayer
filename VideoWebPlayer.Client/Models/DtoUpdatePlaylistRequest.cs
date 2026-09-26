namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request body for updating an existing playlist.
    /// </summary>
    public class DtoUpdatePlaylistRequest
    {
        /// <summary>
        /// Gets or sets the name of the playlist.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the playlist.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the sort mode of the playlist, e.g. <see cref="PlaylistSortModeValues.Manual"/>.
        /// </summary>
        public string? SortMode { get; set; }
    }
}
