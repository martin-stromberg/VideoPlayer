namespace VideoWebPlayer.Services;

/// <summary>
/// Thrown by <see cref="PlaylistService"/> when creating or renaming a playlist would duplicate the name
/// of another playlist already owned by the same user. Having a dedicated type (rather than a plain
/// <see cref="InvalidOperationException"/> matched by message text) lets <c>PlaylistsController</c> map
/// this specific business rule to HTTP 409 Conflict without inspecting the exception message.
/// </summary>
[Serializable]
internal class PlaylistNameAlreadyExistsException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistNameAlreadyExistsException"/> class.
    /// </summary>
    public PlaylistNameAlreadyExistsException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistNameAlreadyExistsException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PlaylistNameAlreadyExistsException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistNameAlreadyExistsException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PlaylistNameAlreadyExistsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
