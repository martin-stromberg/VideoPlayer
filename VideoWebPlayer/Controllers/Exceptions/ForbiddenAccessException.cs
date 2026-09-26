/// <summary>
/// Thrown when an authenticated user requests a media entry they are not authorized for.
/// Distinguishes the missing authorization (mapped to 403 Forbidden) from a missing or invalid
/// credential, which keeps being signalled with <see cref="UnauthorizedAccessException"/> and 401.
/// </summary>
[Serializable]
internal class ForbiddenAccessException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class.
    /// </summary>
    public ForbiddenAccessException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ForbiddenAccessException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ForbiddenAccessException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
