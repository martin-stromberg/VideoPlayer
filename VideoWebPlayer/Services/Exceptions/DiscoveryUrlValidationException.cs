namespace VideoWebPlayer.Services;

/// <summary>
/// Thrown by <see cref="ProgramSettingsService.UpdateGeneralSettingsAsync"/> when the
/// submitted public base URL for discovery answers is neither empty nor a valid absolute
/// <c>http</c>/<c>https</c> URL. Callers (e.g. the admin settings page) can catch this
/// specific type to surface the validation message, instead of treating every
/// <see cref="ArgumentException"/> from the persistence path as a known field error.
/// </summary>
internal class DiscoveryUrlValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryUrlValidationException"/> class.
    /// </summary>
    public DiscoveryUrlValidationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryUrlValidationException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DiscoveryUrlValidationException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryUrlValidationException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DiscoveryUrlValidationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
