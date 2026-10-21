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
/// The outcome of one <see cref="IProgramScheduleService.ReconcileAsync"/> run.
/// </summary>
/// <param name="SlotsDisplaced">How many existing scheduled slots were marked displaced.</param>
/// <param name="SlotsCreated">How many replacement slots were persisted.</param>
/// <param name="FillerSlotsCreated">How many of them came from filler rules.</param>
/// <param name="SkippedNoDuration">How many content elements were skipped because no probed duration exists.</param>
/// <param name="SkippedNoAccess">How many content elements were skipped because they are not released for the whole audience.</param>
/// <param name="OrphanedRules">How many rules referenced material that no longer exists.</param>
/// <param name="DroppedConflicts">How many rule airings were dropped because higher-priority coverage occupied their window.</param>
/// <param name="RemainingGapSeconds">Total seconds of the planning window left uncovered (e.g. no fitting filler).</param>
public readonly record struct ProgramScheduleReconcileSummary(
    int SlotsDisplaced,
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

    /// <summary>
    /// Re-evaluates the channel's program schedule for <c>[utcNow, utcNow + horizon]</c> against the
    /// current rules and media library: scheduled slots that are no longer valid (producing rule
    /// gone or inactive, airing no longer reproducible, referenced media item deleted, content no
    /// longer in the rule's expansion) are marked <see cref="ProgramSlotStatus.Displaced"/> — never
    /// deleted — and the freed windows are re-materialized from the current rules, re-airing
    /// displaced series content in air order via the rule cursor.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the run did.</returns>
    Task<ProgramScheduleReconcileSummary> ReconcileAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the channel's program slots (scheduled and displaced) starting at or after
    /// <paramref name="fromUtc"/>, ordered by start time — for the administration schedule view.
    /// </summary>
    /// <param name="channelId">The channel identifier.</param>
    /// <param name="fromUtc">Include slots whose start is at or after this UTC time.</param>
    /// <param name="maxCount">Maximum number of slots to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upcoming program slots including their status.</returns>
    Task<List<ProgramSlot>> GetScheduleAsync(long channelId, DateTimeOffset fromUtc, int maxCount, CancellationToken cancellationToken = default);
}
