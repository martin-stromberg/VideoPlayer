namespace VideoWebPlayer.Services;

/// <summary>
/// Thrown when a user attempts to access or modify a playlist owned by another user.
/// </summary>
[Serializable]
internal class PlaylistAccessDeniedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistAccessDeniedException"/> class.
    /// </summary>
    public PlaylistAccessDeniedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistAccessDeniedException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PlaylistAccessDeniedException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistAccessDeniedException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PlaylistAccessDeniedException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
