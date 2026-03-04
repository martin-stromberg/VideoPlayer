namespace VideoWebPlayer.Maui.Services.Events;

/// <summary>
/// Event raised when new videos have been scanned.
/// </summary>
public class NewVideosScannedEvent : NotificationEvent
{
    /// <summary>
    /// Gets the ID of the media source where videos were scanned.
    /// </summary>
    public long SourceId { get; }

    /// <summary>
    /// Gets the number of new videos that were scanned.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NewVideosScannedEvent"/> class.
    /// </summary>
    /// <param name="sourceId">The ID of the media source.</param>
    /// <param name="count">The number of new videos.</param>
    public NewVideosScannedEvent(long sourceId, int count)
    {
        SourceId = sourceId;
        Count = count;
    }
}
