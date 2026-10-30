using System;

namespace VideoWebPlayer.Services;

/// <summary>
/// Diagnosis of a segmentation process that ended prematurely on its own — not through
/// <see cref="ILiveStreamSegmenter.StopAsync"/> and well before the job's remaining air time
/// was covered. Reported to the <see cref="LiveStreamCoordinator"/> so the next attempt can
/// switch the input method when the failure was an HTTP transport problem.
/// </summary>
/// <param name="SlotId">The broadcast slot the failed job belonged to.</param>
/// <param name="ExitCode">The process exit code (-1 when it could not be determined).</param>
/// <param name="HttpTransportFailure">
/// Whether stderr classified the exit as an HTTP transport failure of an HTTP input
/// (endpoint unreachable or rejecting) — only then does a fallback to <c>pipe:0</c> help.
/// </param>
/// <param name="OccurredAtUtc">When the exit was detected.</param>
/// <param name="UndecodableInput">
/// Whether stderr classified the input itself as undemuxable (corrupt file or a container
/// layout the input kind cannot deliver, e.g. an MP4 moov atom at the end of a non-seekable
/// pipe) — no retry on any input helps, so the coordinator skips the slot until its end.
/// </param>
/// <param name="SftpInputFailure">
/// Whether stderr classified the exit as a transport/authentication failure of an
/// <c>sftp://</c> input — the next attempt falls back to <c>pipe:0</c>.
/// </param>
public sealed record LiveStreamExitInfo(
    long SlotId,
    int ExitCode,
    bool HttpTransportFailure,
    DateTimeOffset OccurredAtUtc,
    bool UndecodableInput = false,
    bool SftpInputFailure = false);
