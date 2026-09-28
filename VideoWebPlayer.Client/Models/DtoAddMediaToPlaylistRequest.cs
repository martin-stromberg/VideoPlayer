namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request body for adding a media item to a playlist.
    /// </summary>
    public class DtoAddMediaToPlaylistRequest
    {
        /// <summary>
        /// Gets or sets the type of the media item to add, e.g. <see cref="MediaTypeValues.Movie"/>.
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the media item to add.
        /// </summary>
        public long MediaId { get; set; }
    }
}
