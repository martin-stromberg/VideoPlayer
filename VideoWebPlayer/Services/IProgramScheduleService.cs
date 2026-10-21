using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// The outcome of one <see cref="IProgramScheduleService.EnsureCoverageAsync"/> run.
/// </summary>
/// <param name="SlotsCreated">How many program slots were persisted.</param>
/// <param name="FillerSlotsCreated">How many of them came from filler rules.</param>
/// <param name="SkippedNoDuration">How many content elements were skipped because no probed duration exists.</param>
/// <param name="SkippedNoAccess">How many content elements were skipped because they are not released for the whole audience.</param>
/// <param name="OrphanedRules">How many rules referenced material that no longer exists.</param>
/// <param name="DroppedConflicts">How many rule airings were dropped because a higher-priority airing occupied their window.</param>
/// <param name="RemainingGapSeconds">Total seconds of the planning window left uncovered (e.g. no fitting filler).</param>
public readonly record struct ProgramScheduleRunSummary(
    int SlotsCreated,
    int FillerSlotsCreated,
    int SkippedNoDuration,
    int SkippedNoAccess,
    int OrphanedRules,
    int DroppedConflicts,
    long RemainingGapSeconds);

/// <summary>
/// Generates the rolling, gapless program schedule ("Sendeplan") of the delivered
/// <see cref="TVChannel"/> from its active <see cref="ProgramRule"/>s.
/// </summary>
public interface IProgramScheduleService
{
    /// <summary>
    /// Extends the channel's program schedule so it covers the configured horizon starting at
    /// <paramref name="utcNow"/>. Existing slots are never modified — the run only appends coverage
    /// (extend-only; reconciling the plan after rule or media changes is a separate concern).
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the run did.</returns>
    Task<ProgramScheduleRunSummary> EnsureCoverageAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}
