using System.Threading;
using System.Threading.Tasks;

namespace VideoWebPlayer.Services;

/// <summary>
/// Owns the single continuously running HLS segmentation process of the automated TV channel:
/// an ffmpeg process transcoding the currently airing broadcast into HLS segments. The
/// implementation holds the process state; callers (the live stream coordinator) only express
/// which job should be running.
/// </summary>
public interface ILiveStreamSegmenter
{
    /// <summary>
    /// Gets a value indicating whether a segmentation process is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the slot identifier of the current job, or <see langword="null"/> when no job is running.
    /// </summary>
    long? CurrentSlotId { get; }

    /// <summary>
    /// Gets the playback position the running process is expected to have reached, or
    /// <see langword="null"/> when no job is running.
    /// </summary>
    double? ExpectedPositionSeconds { get; }

    /// <summary>
    /// Starts a segmentation job. Any previously running job is stopped first.
    /// </summary>
    /// <param name="job">The job to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(LiveStreamJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the running segmentation process, if any.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
