namespace VideoWebPlayer.Configuration
{
    /// <summary>
    /// Strongly typed configuration options for the playlist feature.
    /// </summary>
    public class PlaylistSettings
    {
        /// <summary>
        /// Gets or sets the maximum number of playlists a single user may create. <c>null</c> means unlimited.
        /// </summary>
        public int? MaxPlaylistsPerUser { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of entries a single playlist may contain. <c>null</c> means unlimited.
        /// Enforced in <see cref="VideoWebPlayer.Services.PlaylistService.AddMediaToPlaylistAsync"/>, counting cascade entries.
        /// </summary>
        public int? MaxPlaylistItemCount { get; set; }
    }
}
