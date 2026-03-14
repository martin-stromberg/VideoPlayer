namespace VideoWebPlayer.Maui.Services.Events;

/// <summary>
/// Event raised when a download has been deleted.
/// </summary>
public class DownloadDeletedEvent : NotificationEvent
{
    /// <summary>
    /// Gets the ID of the deleted download.
    /// </summary>
    public long VideoId { get; }

    /// <summary>
    /// Gets the type of the deleted video (movie or episode).
    /// </summary>
    public string VideoType { get; }

    /// <summary>
    /// Gets the title of the deleted download.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadDeletedEvent"/> class.
    /// </summary>
    /// <param name="videoId">The ID of the deleted video.</param>
    /// <param name="videoType">The type of the deleted video.</param>
    /// <param name="title">The title of the deleted download.</param>
    public DownloadDeletedEvent(long videoId, string videoType, string title)
    {
        VideoId = videoId;
        VideoType = videoType ?? throw new ArgumentNullException(nameof(videoType));
        Title = title ?? string.Empty;
    }
}
