namespace VideoWebPlayer.Services;

/// <summary>
/// Downloads and installs the ffmpeg/ffprobe binaries into the convention directory
/// <c>tools/ffmpeg/</c> next to the application and tracks the job state for the UI.
/// </summary>
public interface IFfmpegToolDownloadService
{
    /// <summary>
    /// Gets the current download job state.
    /// </summary>
    FfmpegToolDownloadJobSnapshot GetSnapshot();

    /// <summary>
    /// Marks the finished job result the caller actually observed — identified by
    /// <paramref name="snapshotId"/> — as seen: the snapshot returns to
    /// <see cref="FfmpegToolDownloadJobStatus.Idle"/> so a later page visit does not
    /// re-display the stale result. The reset only happens while the current snapshot is
    /// still that same, no longer active job: a late acknowledge can never erase a newer,
    /// never-displayed result. An active job is never touched.
    /// </summary>
    /// <param name="snapshotId">The <see cref="FfmpegToolDownloadJobSnapshot.Id"/> the
    /// caller observed and now acknowledges.</param>
    void AcknowledgeTerminalState(Guid snapshotId);

    /// <summary>
    /// Starts a download/install job when no other job is active. The caller must be an
    /// administrator — the check is enforced server-side inside the service.
    /// </summary>
    /// <param name="userId">The requesting user; must be an administrator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether a new job was started and the snapshot after the attempt.</returns>
    Task<FfmpegToolDownloadStartResult> StartDownloadAsync(string userId, CancellationToken cancellationToken = default);
}
