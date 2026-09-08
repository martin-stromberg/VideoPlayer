namespace VideoWebPlayer.Services;

/// <summary>
/// Thrown when a manual-sort-order operation (single or batch reorder) is attempted on a playlist whose
/// <see cref="Data.PlaylistSortMode"/> is not <see cref="Data.PlaylistSortMode.Manual"/>. Having a
/// dedicated type (rather than a plain <see cref="InvalidOperationException"/>) lets
/// <c>PlaylistsController</c> map this specific business rule to one consistent HTTP status code for both
/// the single-entry and batch reorder endpoints, instead of the two endpoints diverging (400 vs. 409)
/// depending on which endpoint happened to pass a conflict-mapping delegate.
/// </summary>
[Serializable]
internal class PlaylistNotInManualSortModeException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistNotInManualSortModeException"/> class.
    /// </summary>
    public PlaylistNotInManualSortModeException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistNotInManualSortModeException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PlaylistNotInManualSortModeException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistNotInManualSortModeException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PlaylistNotInManualSortModeException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
