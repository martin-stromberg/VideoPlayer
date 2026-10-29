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
/// <param name="BlockedByAgeWindow">How many scanned candidates were blocked by the youth-protection window.</param>
public readonly record struct ProgramScheduleRunSummary(
    int SlotsCreated,
    int FillerSlotsCreated,
    int SkippedNoDuration,
    int SkippedNoAccess,
    int OrphanedRules,
    int DroppedConflicts,
    long RemainingGapSeconds,
    int BlockedByAgeWindow);

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
/// <param name="BlockedByAgeWindow">How many scanned candidates were blocked by the youth-protection window.</param>
public readonly record struct ProgramScheduleReconcileSummary(
    int SlotsDisplaced,
    int SlotsCreated,
    int FillerSlotsCreated,
    int SkippedNoDuration,
    int SkippedNoAccess,
    int OrphanedRules,
    int DroppedConflicts,
    long RemainingGapSeconds,
    int BlockedByAgeWindow);

/// <summary>
/// Per-rule planning diagnosis for the administration view: how much content of a
/// <see cref="ProgramRule"/>'s material is currently schedulable, and — when nothing is —
/// why not. Computed on demand with the same expansion logic the schedule run uses.
/// </summary>
/// <param name="RuleId">The rule the diagnosis belongs to.</param>
/// <param name="MaterialMissing"><see langword="true"/> when the referenced material no longer
/// resolves (entity deleted; for genre material also when the stored genres resolve to no
/// usable canonical names or no genre rows inside the source filter).</param>
/// <param name="CandidateCount">How many content elements the material expansion considered.</param>
/// <param name="SchedulableCount">How many of them are schedulable (probed duration and released
/// for the whole audience).</param>
/// <param name="NoMediaItemCount">Candidates without any linked media item.</param>
/// <param name="ProbePendingCount">Candidates whose media item has no duration yet and whose
/// probe is still pending or retrying (attempts below the configured maximum).</param>
/// <param name="ProbeExhaustedCount">Candidates whose media item has no duration and whose
/// probe attempt budget is exhausted (permanently failed).</param>
/// <param name="NoAccessCount">Candidates with a probed duration that are not released for the
/// whole (non-admin) audience.</param>
/// <param name="AgeWindowBlockedCount">Schedulable candidates that are not age-appropriate for
/// the rule's evaluation time (its next occurrence start) under the configured
/// youth-protection window.</param>
public readonly record struct ProgramRuleDiagnosis(
    long RuleId,
    bool MaterialMissing,
    int CandidateCount,
    int SchedulableCount,
    int NoMediaItemCount,
    int ProbePendingCount,
    int ProbeExhaustedCount,
    int NoAccessCount,
    int AgeWindowBlockedCount);

/// <summary>
/// The outcome of <see cref="IProgramScheduleService.ResetScheduleAsync"/>.
/// </summary>
/// <param name="SlotsDeleted">How many program slots (scheduled and displaced) were deleted.</param>
/// <param name="CursorsDeleted">How many rule cursors (continuation state) were deleted.</param>
public readonly record struct ProgramScheduleResetResult(int SlotsDeleted, int CursorsDeleted);

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

    /// <summary>
    /// Deletes all <see cref="ProgramSlotStatus.Displaced"/> slots of the channel — housekeeping
    /// for re-evaluation leftovers that only exist for traceability. Administrator-only.
    /// </summary>
    /// <param name="userId">The requesting user; must be an administrator.</param>
    /// <param name="channelId">The channel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many displaced slots were deleted.</returns>
    Task<int> DeleteDisplacedSlotsAsync(string userId, long channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the channel's expired one-off (<see cref="ProgramRuleValidityMode.OneTime"/>) program
    /// rules — every rule whose <see cref="ProgramRule.ValidTo"/> lies before the local day of
    /// <paramref name="utcNow"/>; one-off rules without a Bis date and all recurring validity
    /// windows never expire. Called by the schedule worker inside the
    /// <c>TvSenderEnabled</c> block before <see cref="ReconcileAsync"/> (not administrator-bound);
    /// cursors and join rows cascade, the orphaned slots stay as history and are marked displaced
    /// by the following reconcile run.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many expired rules were deleted.</returns>
    Task<int> PurgeExpiredRulesAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes proven self-displacement duplicates: every <see cref="ProgramSlotStatus.Displaced"/>
    /// slot that has a scheduled twin on the same channel — identical start, end, producing rule,
    /// <see cref="ProgramSlot.IsClipFill"/> flag and content (same media item, or same
    /// movie/episode). The scheduled twin covers the deleted interval completely, so the
    /// occupancy evidence the next <see cref="ReconcileAsync"/> sees is unchanged — genuine
    /// displacement history (no twin, different content, foreign rule) is kept. Called by the
    /// schedule worker inside the <c>TvSenderEnabled</c> block before <see cref="ReconcileAsync"/>
    /// (not administrator-bound); for a full sweep of all displaced rows use
    /// <see cref="DeleteDisplacedSlotsAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many displaced slots were deleted.</returns>
    Task<int> CleanupSelfDisplacedSlotsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the channel's whole program schedule — every slot regardless of status plus all
    /// rule cursors — so the next schedule run plans completely fresh. Administrator-only.
    /// </summary>
    /// <param name="userId">The requesting user; must be an administrator.</param>
    /// <param name="channelId">The channel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many slots and cursors were deleted.</returns>
    Task<ProgramScheduleResetResult> ResetScheduleAsync(string userId, long channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the per-rule planning diagnosis for the administration view: every rule of the
    /// channel (including inactive ones) is expanded with the same material logic a schedule run
    /// uses, and the skip reasons are counted per rule. Administrator-only.
    /// </summary>
    /// <param name="userId">The requesting user; must be an administrator.</param>
    /// <param name="channelId">The channel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The diagnosis per rule id.</returns>
    Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>> GetRuleDiagnosesAsync(string userId, long channelId, CancellationToken cancellationToken = default);
}
