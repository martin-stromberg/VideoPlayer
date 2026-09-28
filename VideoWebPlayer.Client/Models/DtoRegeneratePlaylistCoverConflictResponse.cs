namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response returned when regenerating a playlist's cover would replace a cover image the user
    /// uploaded themselves and requires the caller to confirm.
    /// </summary>
    public class DtoRegeneratePlaylistCoverConflictResponse
    {
        /// <summary>
        /// Gets or sets whether the caller must confirm that the uploaded cover image is replaced by an
        /// automatically generated one before the regeneration can be applied.
        /// </summary>
        public bool IsUploadedCoverReplacementConfirmationRequired { get; set; }
    }
}
