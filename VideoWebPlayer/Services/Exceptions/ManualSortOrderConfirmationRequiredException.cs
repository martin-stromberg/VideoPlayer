namespace VideoWebPlayer.Services;

/// <summary>
/// Thrown by <see cref="PlaylistService.ChangeSortModeAsync"/> when switching a playlist from
/// <see cref="Data.PlaylistSortMode.Manual"/> to <see cref="Data.PlaylistSortMode.ByReleaseDate"/> would
/// discard the existing manual order and the caller has not confirmed the loss via
/// <c>confirmLossOfManualOrder</c>. Callers (e.g. <c>PlaylistsController</c>) can catch this specific type
/// to surface a confirmation prompt, instead of matching on the message text of a plain
/// <see cref="InvalidOperationException"/>.
/// </summary>
[Serializable]
internal class ManualSortOrderConfirmationRequiredException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ManualSortOrderConfirmationRequiredException"/> class.
    /// </summary>
    public ManualSortOrderConfirmationRequiredException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualSortOrderConfirmationRequiredException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ManualSortOrderConfirmationRequiredException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualSortOrderConfirmationRequiredException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ManualSortOrderConfirmationRequiredException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
