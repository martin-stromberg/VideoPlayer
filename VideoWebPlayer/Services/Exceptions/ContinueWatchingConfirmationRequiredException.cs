namespace VideoWebPlayer.Services;

/// <summary>
/// Thrown by <see cref="PlaylistService.RemoveMediaFromPlaylistAsync"/> when removing a single entry from
/// a playlist would affect a <see cref="Data.ContinueWatchingEntry"/> bound to that same playlist (a
/// Weiterschauen-Eintrag referencing the entry being removed, with <see cref="Data.ContinueWatchingEntry.PlaylistId"/>
/// pointing at the same playlist) and the caller has not confirmed the removal via
/// <c>confirmContinueWatchingRemoval</c>. Mirrors <see cref="ManualSortOrderConfirmationRequiredException"/>'s
/// role for the sort-mode-change confirmation: callers (e.g. <c>PlaylistsController</c>) can catch this
/// specific type to surface a confirmation prompt instead of matching on the message text of a plain
/// <see cref="InvalidOperationException"/>.
/// </summary>
[Serializable]
internal class ContinueWatchingConfirmationRequiredException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContinueWatchingConfirmationRequiredException"/> class.
    /// </summary>
    public ContinueWatchingConfirmationRequiredException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinueWatchingConfirmationRequiredException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ContinueWatchingConfirmationRequiredException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinueWatchingConfirmationRequiredException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ContinueWatchingConfirmationRequiredException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
