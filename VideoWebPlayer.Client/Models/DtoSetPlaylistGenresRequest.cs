namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request DTO for manually overriding a playlist's genres.
    /// </summary>
    public class DtoSetPlaylistGenresRequest
    {
        /// <summary>
        /// Gets or sets the genre ids to assign to the playlist, replacing every automatically derived or
        /// previously manually assigned genre. Unknown ids are silently ignored server-side.
        /// </summary>
        public long[] GenreIds { get; set; } =
            Array.Empty<long>();
    }
}
