using VideoWebPlayer.Maui.Models;

namespace VideoWebPlayer.Maui.Services.Events;

/// <summary>
/// Event raised when a download has been completed.
/// </summary>
public class DownloadCompletedEvent : NotificationEvent
{
    /// <summary>
    /// Gets the completed download.
    /// </summary>
    public DownloadedVideo Download { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadCompletedEvent"/> class.
    /// </summary>
    /// <param name="download">The completed download.</param>
    public DownloadCompletedEvent(DownloadedVideo download)
    {
        Download = download ?? throw new ArgumentNullException(nameof(download));
    }
}
