namespace VideoWebPlayer.Services;

/// <summary>
/// Thrown by <see cref="PlaylistService.GeneratePlaylistCoverAsync"/> when regenerating a playlist's cover
/// would replace a cover image the user uploaded themselves
/// (<see cref="Data.Playlist.CoverPictureIsUserUploaded"/>) and the caller has not confirmed the
/// replacement via <c>confirmReplaceUploadedCover</c>. Mirrors
/// <see cref="ContinueWatchingConfirmationRequiredException"/> and
/// <see cref="ManualSortOrderConfirmationRequiredException"/>: callers (e.g. <c>PlaylistsController</c>)
/// can catch this specific type to surface a confirmation prompt instead of matching on the message text
/// of a plain <see cref="InvalidOperationException"/>.
/// </summary>
[Serializable]
internal class UploadedCoverReplacementConfirmationRequiredException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UploadedCoverReplacementConfirmationRequiredException"/> class.
    /// </summary>
    public UploadedCoverReplacementConfirmationRequiredException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadedCoverReplacementConfirmationRequiredException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public UploadedCoverReplacementConfirmationRequiredException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadedCoverReplacementConfirmationRequiredException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public UploadedCoverReplacementConfirmationRequiredException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
