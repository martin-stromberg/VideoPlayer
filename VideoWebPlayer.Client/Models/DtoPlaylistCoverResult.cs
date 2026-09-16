namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// The response of a playlist cover upload, regeneration or delete operation.
    /// </summary>
    public class DtoPlaylistCoverResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation produced a new (or cleared) cover.
        /// <see langword="false"/> for a regeneration that found no source images to compose a collage from.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a user-facing message describing the outcome.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the id of the resulting cover picture, or <c>null</c> when <see cref="Success"/> is
        /// <see langword="false"/>, or after a delete.
        /// </summary>
        public long? PictureId { get; set; }
    }
}
