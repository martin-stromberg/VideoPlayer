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
public sealed record LiveStreamExitInfo(long SlotId, int ExitCode, bool HttpTransportFailure, DateTimeOffset OccurredAtUtc);
