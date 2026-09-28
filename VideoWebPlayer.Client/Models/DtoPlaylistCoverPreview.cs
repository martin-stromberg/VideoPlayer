namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// The response of the playlist cover preview: the automatically composed collage of the playlist's
    /// current contents, returned as image data <b>without</b> being saved anywhere. It only becomes the
    /// playlist's cover once the owner applies it (regenerate endpoint).
    /// </summary>
    public class DtoPlaylistCoverPreview
    {
        /// <summary>
        /// Gets or sets a value indicating whether a collage could be composed. <see langword="false"/> when the
        /// playlist has no entries with a poster image to compose one from.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a user-facing message describing the outcome (e.g. why no preview is available).
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of <see cref="ImageData"/> (always <c>image/jpeg</c> for a generated collage),
        /// or <c>null</c> when <see cref="Success"/> is <see langword="false"/>.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Gets or sets the raw image bytes of the collage (serialized as base64 in JSON), or <c>null</c> when
        /// <see cref="Success"/> is <see langword="false"/>.
        /// </summary>
        public byte[]? ImageData { get; set; }
    }
}
