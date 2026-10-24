using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements <see cref="IProgramScheduleService"/>: generates the rolling, gapless program
/// schedule ("Sendeplan") of the delivered channel from its active <see cref="ProgramRule"/>s.
/// <para>
/// The run is extend-only: it computes <c>coverageStart = max(now, last covered slot end)</c> and
/// fills <c>[coverageStart, now + horizon]</c>. Existing slots are never modified — reconciling
/// the plan after rule or media changes is handled separately. Rule airings conflicting on time
/// are resolved by priority (higher wins, ties by lower rule id, then earlier start); remaining
/// gaps are padded with filler material. Only content with a probed <see cref="MediaItem.Duration"/>
/// that is released for the whole (non-admin) audience is scheduled. Orphaned rule materials (no
/// foreign key on <see cref="ProgramRule.MaterialId"/>) expand to nothing and are logged.
/// </para>
/// </summary>
public sealed class ProgramScheduleService : IProgramScheduleService
{
    // How far before the coverage start an airing may begin and still be materialized — covers
    // broadcasts that started "yesterday" and spill into the planning window.
    private static readonly TimeSpan OccurrenceLookback = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _db;
    private readonly IProgramRuleService _ruleService;
    private readonly ProgramSettingsService _settings;
    private readonly ILogger<ProgramScheduleService> _logger;
    private readonly int _maxProbeAttempts;
    private readonly TimeSpan _trailerLead;
    private readonly TimeSpan _minStartRemain;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="ruleService">Channel access.</param>
    /// <param name="settings">Program settings (horizon, federal state).</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="probeOptions">Media probe options — the attempt limit classifies exhausted
    /// duration probes in the rule diagnosis; when <see langword="null"/> the defaults apply.</param>
    /// <param name="scheduleOptions">Program schedule options — the trailer lead window; when
    /// <see langword="null"/> the defaults apply.</param>
    public ProgramScheduleService(
        ApplicationDbContext db,
        IProgramRuleService ruleService,
        ProgramSettingsService settings,
        ILogger<ProgramScheduleService> logger,
        IOptions<MediaProbeOptions>? probeOptions = null,
        IOptions<ProgramScheduleOptions>? scheduleOptions = null)
    {
        _db = db;
        _ruleService = ruleService;
        _settings = settings;
        _logger = logger;
        _maxProbeAttempts = (probeOptions?.Value ?? new MediaProbeOptions()).EffectiveMaxAttempts;
        var schedule = scheduleOptions?.Value ?? new ProgramScheduleOptions();
        _trailerLead = schedule.EffectiveTrailerLead;
        _minStartRemain = schedule.EffectiveMinStartRemain;
    }

    /// <inheritdoc />
    public async Task<ProgramScheduleRunSummary> EnsureCoverageAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var channel = await _ruleService.GetOrCreateChannelAsync(cancellationToken);
        var rules = await _db.ProgramRules
            .Include(r => r.Genres)
            .Include(r => r.MediaSourceFilters)
            .Where(r => r.TVChannelId == channel.Id && r.IsActive)
            .ToListAsync(cancellationToken);

        var horizonEnd = utcNow + await _settings.GetProgramScheduleHorizonAsync(cancellationToken);
        var lastEnd = await _db.ProgramSlots
            .Where(s => s.TVChannelId == channel.Id && s.Status == ProgramSlotStatus.Scheduled)
            .MaxAsync(s => (DateTime?)s.End, cancellationToken);

        var coverageStart = lastEnd is { } end && end > utcNow.UtcDateTime ? end : utcNow.UtcDateTime;
        var summary = new SummaryCounters();
        if (coverageStart >= horizonEnd.UtcDateTime)
            return summary.ToSummary();

        var access = await LoadAccessContextAsync(cancellationToken);
        var federalState = await _settings.GetHolidayFederalStateAsync(cancellationToken);
        var ruleIds = rules.Select(r => r.Id).ToList();
        var cursors = (await _db.ProgramRuleCursors
                .Where(c => ruleIds.Contains(c.ProgramRuleId))
                .ToListAsync(cancellationToken))
            .GroupBy(c => c.ProgramRuleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProgramRuleCursor>)g.ToList());

        var states = new Dictionary<long, RuleState>();
        foreach (var rule in rules)
        {
            // Trailer material expands only after the occurrences are materialized — its content
            // set depends on the freshly computed movie airings of this very run.
            var items = rule.MaterialType == ProgramRuleMaterialType.Trailer
                ? new List<SchedulableItem>()
                : await ExpandMaterialAsync(rule, access, summary, cancellationToken);
            states[rule.Id] = new RuleState(rule, items, cursors.GetValueOrDefault(rule.Id) ?? []);
        }

        var blocked = (await _db.ProgramSlots
                .Where(s => s.TVChannelId == channel.Id && s.Status == ProgramSlotStatus.Scheduled && s.End > coverageStart && s.Start < horizonEnd.UtcDateTime)
                .Select(s => new { s.Start, s.End })
                .ToListAsync(cancellationToken))
            .Select(s => new Interval(s.Start, s.End))
            .ToList();

        var newSlots = new List<ProgramSlot>();
        var occurrences = BuildOccurrences(rules.Where(r => r.RuleType != ProgramRuleType.Filler), coverageStart, horizonEnd, federalState);
        var boundStarts = BoundStarts(occurrences, states);
        foreach (var occurrence in occurrences.OrderByDescending(o => o.Rule.Priority).ThenBy(o => o.Rule.Id).ThenBy(o => o.StartUtc))
            MaterializeOccurrence(occurrence, states, blocked, newSlots, coverageStart, BoundaryAfter(boundStarts, occurrence.StartUtc, occurrence.Rule.Priority), summary);

        var movieAirings = await LoadMovieAiringsAsync(channel.Id, coverageStart, newSlots, cancellationToken);
        await ExpandTrailerStatesAsync(states, cursors, access, movieAirings, coverageStart, summary, cancellationToken);
        FillGaps(states, rules, blocked, newSlots, coverageStart, horizonEnd.UtcDateTime, summary, movieAirings);

        PersistCursors(states, cursors);
        _db.ProgramSlots.AddRange(newSlots);
        await _db.SaveChangesAsync(cancellationToken);

        var result = summary.ToSummary();
        _logger.LogInformation(
            "[ProgramSchedule] {Created} Sendeplaetze erzeugt ({Filler} Fueller), {Conflicts} Ausstrahlungen konfliktverworfen, {GapSeconds}s Restluecke.",
            result.SlotsCreated, result.FillerSlotsCreated, result.DroppedConflicts, result.RemainingGapSeconds);
        return result;
    }

    /// <inheritdoc />
    public async Task<ProgramScheduleReconcileSummary> ReconcileAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var channel = await _ruleService.GetOrCreateChannelAsync(cancellationToken);
        var rules = await _db.ProgramRules
            .Include(r => r.Genres)
            .Include(r => r.MediaSourceFilters)
            .Where(r => r.TVChannelId == channel.Id)
            .ToListAsync(cancellationToken);
        var rulesById = rules.ToDictionary(r => r.Id);
        var activeRules = rules.Where(r => r.IsActive).ToList();

        var horizonEnd = utcNow + await _settings.GetProgramScheduleHorizonAsync(cancellationToken);
        var coverageStart = utcNow.UtcDateTime;

        var summary = new SummaryCounters();
        var access = await LoadAccessContextAsync(cancellationToken);
        var federalState = await _settings.GetHolidayFederalStateAsync(cancellationToken);

        // States (expansion + cursor) are built for every rule — including inactive ones — because
        // displaced slots of inactive rules still need a cursor rewind so reactivation continues
        // in air order instead of jumping ahead.
        var ruleIds = rules.Select(r => r.Id).ToList();
        var cursors = (await _db.ProgramRuleCursors
                .Where(c => ruleIds.Contains(c.ProgramRuleId))
                .ToListAsync(cancellationToken))
            .GroupBy(c => c.ProgramRuleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProgramRuleCursor>)g.ToList());

        var airingsByMovie = await LoadMovieAiringsAsync(channel.Id, coverageStart, [], cancellationToken);
        var states = new Dictionary<long, RuleState>();
        foreach (var rule in rules)
        {
            var items = await ExpandMaterialAsync(rule, access, summary, airingsByMovie, coverageStart, cancellationToken);
            states[rule.Id] = new RuleState(rule, items, cursors.GetValueOrDefault(rule.Id) ?? []);
        }

        // Scheduled slots reaching into the lookback window — wide enough to capture the head slot
        // of airings that are still running. No upper bound on Start: an airing overshooting the
        // horizon must be loaded completely, otherwise the truncated tail fails the sequence check
        // and gets displaced — which then re-materializes the same positions and piles up
        // overlapping displaced duplicates run over run.
        var loadFrom = coverageStart - OccurrenceLookback;
        var scheduled = await _db.ProgramSlots
            .Where(s => s.TVChannelId == channel.Id && s.Status == ProgramSlotStatus.Scheduled && s.End > loadFrom)
            .OrderBy(s => s.Start)
            .ToListAsync(cancellationToken);

        // Invader evidence for the reproducibility check: an airing that starts later than its
        // nominal occurrence start is only plausible when an occupied slot covered that start —
        // grouped by the invader's exact end (the shifted airing starts there contiguously).
        var invaderStarts = scheduled
            .GroupBy(s => s.End)
            .ToDictionary(g => g.Key, g => g.Min(s => s.Start));

        var occurrences = BuildOccurrences(activeRules.Where(r => r.RuleType != ProgramRuleType.Filler), coverageStart, horizonEnd, federalState);
        var occurrenceStarts = occurrences
            .GroupBy(o => o.Rule.Id)
            .ToDictionary(g => g.Key, g => g.Select(o => o.StartUtc).ToHashSet());
        var boundStarts = BoundStarts(occurrences, states);

        // Non-filler slots group into airings (maximal contiguous runs of the same producing
        // rule); filler slots are validated individually.
        var airings = GroupAirings(scheduled.Where(s => s.RuleType != ProgramRuleType.Filler).ToList());
        var fillerSlots = scheduled.Where(s => s.RuleType == ProgramRuleType.Filler).ToList();

        var displaced = new List<ProgramSlot>();
        var displacedRuleIds = new HashSet<long>();

        void Displace(ProgramSlot slot)
        {
            if (slot.End <= coverageStart)
                return; // fully aired — stays part of history
            slot.Status = ProgramSlotStatus.Displaced;
            displaced.Add(slot);
        }

        // Validate each airing; on the first invalid airing of a rule the whole remaining future of
        // that rule is displaced (sequence context is cursor-based — later airings depend on it).
        foreach (var group in airings.GroupBy(a => a[0].ProgramRuleId))
        {
            var ordered = group.OrderBy(a => a[0].Start).ToList();
            var firstInvalid = -1;
            for (var i = 0; i < ordered.Count && firstInvalid < 0; i++)
            {
                var airing = ordered[i];
                if (!airing.Any(s => s.End > coverageStart))
                    continue; // fully aired — history

                var rule = airing[0].ProgramRuleId is { } rid ? rulesById.GetValueOrDefault(rid) : null;
                var invalid = rule is null || !rule.IsActive
                    || !occurrenceStarts.TryGetValue(rule.Id, out var starts)
                    || !AiringReproducible(airing, states[rule.Id], starts, boundStarts, invaderStarts, _minStartRemain);
                if (invalid)
                    firstInvalid = i;
            }

            if (firstInvalid >= 0)
            {
                foreach (var airing in ordered.Skip(firstInvalid))
                {
                    foreach (var slot in airing)
                        Displace(slot);
                    if (airing[0].ProgramRuleId is { } displacedRuleId)
                        displacedRuleIds.Add(displacedRuleId);
                }
            }
        }

        foreach (var slot in fillerSlots.Where(s => s.End > coverageStart))
        {
            var rule = slot.ProgramRuleId is { } fillerRuleId ? rulesById.GetValueOrDefault(fillerRuleId) : null;
            var invalid = rule is null || !rule.IsActive
                || !states.TryGetValue(rule.Id, out var state)
                || !state.Items.Any(i => i.MediaItemId == slot.MediaItemId);
            if (invalid)
            {
                Displace(slot);
                if (slot.ProgramRuleId is { } fillerRule)
                    displacedRuleIds.Add(fillerRule);
            }
        }

        // Cursor rewind: the earliest still-expandable displaced content per rule (and per
        // strand inside interleaved rules) gets re-aired at the next free occurrence of the
        // same rule context — air order continues without a jump.
        foreach (var ruleId in displacedRuleIds)
        {
            if (states.TryGetValue(ruleId, out var state))
                RewindCursor(state, displaced.Where(s => s.ProgramRuleId == ruleId).ToList(), cursors.GetValueOrDefault(ruleId) ?? []);
        }

        // Re-materialization over the freed windows. Kept scheduled slots block — except they lose
        // to a higher-priority occurrence (filler always loses; otherwise priority, then lower rule id).
        var keptSlots = scheduled.Where(s => s.Status == ProgramSlotStatus.Scheduled && s.End > coverageStart).ToList();
        var newSlots = new List<ProgramSlot>();
        var blocked = keptSlots.Select(s => new Interval(s.Start, s.End)).ToList();

        void DisplaceRuleFuture(long ruleId, DateTime fromStartUtc)
        {
            foreach (var airing in airings.Where(a => a[0].ProgramRuleId == ruleId && a[0].Start >= fromStartUtc))
            {
                foreach (var slot in airing)
                {
                    if (slot.End <= coverageStart || slot.Status != ProgramSlotStatus.Scheduled)
                        continue;
                    slot.Status = ProgramSlotStatus.Displaced;
                    displaced.Add(slot);
                    keptSlots.Remove(slot);
                    blocked.RemoveAll(b => b.Start == slot.Start && b.End == slot.End);
                }
            }

            foreach (var slot in fillerSlots.Where(s => s.ProgramRuleId == ruleId && s.Start >= fromStartUtc && s.Status == ProgramSlotStatus.Scheduled))
            {
                slot.Status = ProgramSlotStatus.Displaced;
                displaced.Add(slot);
                keptSlots.Remove(slot);
                blocked.RemoveAll(b => b.Start == slot.Start && b.End == slot.End);
            }

            if (states.TryGetValue(ruleId, out var state))
                RewindCursor(state, displaced.Where(s => s.ProgramRuleId == ruleId).ToList(), cursors.GetValueOrDefault(ruleId) ?? []);
        }

        bool Evicts(ProgramRule winner, ProgramSlot kept)
        {
            if (kept.RuleType == ProgramRuleType.Filler)
                return true;
            var loser = kept.ProgramRuleId is { } rid ? rulesById.GetValueOrDefault(rid) : null;
            if (loser is null)
                return true;
            return winner.Priority > loser.Priority
                || (winner.Priority == loser.Priority && winner.Id < loser.Id);
        }

        foreach (var occurrence in occurrences.OrderByDescending(o => o.Rule.Priority).ThenBy(o => o.Rule.Id).ThenBy(o => o.StartUtc))
        {
            var losers = MaterializeOccurrenceEvicting(occurrence, states, keptSlots, blocked, newSlots, coverageStart, BoundaryAfter(boundStarts, occurrence.StartUtc, occurrence.Rule.Priority), summary, Evicts);
            if (losers is not null && losers.Count > 0)
            {
                foreach (var loser in losers)
                {
                    if (loser.ProgramRuleId is not { } loserRuleId)
                        continue;
                    var airing = airings.FirstOrDefault(a => a.Contains(loser));
                    // A kept slot can only lose starting inside this occurrence's window —
                    // slots reaching across its start are overshooting tails that air to the
                    // end (the materialization already started behind them).
                    if (airing is not null)
                    {
                        DisplaceRuleFuture(loserRuleId, airing[0].Start);
                    }
                    else if (loser.Status == ProgramSlotStatus.Scheduled && loser.End > coverageStart)
                    {
                        // Ungrouped loser (filler slot): displaced individually — filler has no
                        // sequence context that would justify suffix displacement.
                        loser.Status = ProgramSlotStatus.Displaced;
                        displaced.Add(loser);
                        keptSlots.Remove(loser);
                        blocked.RemoveAll(b => b.Start == loser.Start && b.End == loser.End);
                        if (states.TryGetValue(loserRuleId, out var loserState))
                            RewindCursor(loserState, [loser], cursors.GetValueOrDefault(loserRuleId) ?? []);
                    }
                }
            }
        }

        var leadAirings = await BuildMovieAiringsFromSlotsAsync(keptSlots.Concat(newSlots), coverageStart, cancellationToken);
        FillGaps(states, activeRules, blocked, newSlots, coverageStart, horizonEnd.UtcDateTime, summary, leadAirings);

        PersistCursors(states, cursors);
        _db.ProgramSlots.AddRange(newSlots);
        await _db.SaveChangesAsync(cancellationToken);

        var result = new ProgramScheduleReconcileSummary(
            displaced.Count, summary.SlotsCreated, summary.FillerSlotsCreated,
            summary.SkippedNoDuration, summary.SkippedNoAccess, summary.OrphanedRules,
            summary.DroppedConflicts, summary.RemainingGapSeconds);
        _logger.LogInformation(
            "[ProgramSchedule] Re-Bewertung: {Displaced} Sendeplaetze verdrängt, {Created} erzeugt ({Filler} Fueller).",
            result.SlotsDisplaced, result.SlotsCreated, result.FillerSlotsCreated);
        return result;
    }

    /// <inheritdoc />
    public async Task<List<ProgramSlot>> GetScheduleAsync(long channelId, DateTimeOffset fromUtc, int maxCount, CancellationToken cancellationToken = default)
        => await _db.ProgramSlots
            .AsNoTracking()
            .Where(s => s.TVChannelId == channelId && s.Start >= fromUtc.UtcDateTime)
            .OrderBy(s => s.Start)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>> GetRuleDiagnosesAsync(string userId, long channelId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);

        var rules = await _db.ProgramRules
            .AsNoTracking()
            .Include(r => r.Genres)
            .Include(r => r.MediaSourceFilters)
            .Where(r => r.TVChannelId == channelId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var access = await LoadAccessContextAsync(cancellationToken);
        var airingsByMovie = await LoadMovieAiringsAsync(channelId, DateTime.UtcNow, [], cancellationToken);
        var diagnoses = new Dictionary<long, ProgramRuleDiagnosis>();
        foreach (var rule in rules)
        {
            // Fresh counters per rule — the same expansion the schedule run uses, with the skip
            // reasons tallied into a per-rule diagnosis instead of the run summary.
            var counters = new SummaryCounters();
            var items = await ExpandMaterialAsync(rule, access, counters, airingsByMovie, DateTime.UtcNow, cancellationToken);
            diagnoses[rule.Id] = new ProgramRuleDiagnosis(
                rule.Id,
                counters.MaterialMissing > 0,
                counters.Candidates,
                items.Count,
                counters.NoMediaItem,
                counters.ProbePending,
                counters.ProbeExhausted,
                counters.SkippedNoAccess);
        }

        return diagnoses;
    }

    /// <inheritdoc />
    public async Task<int> DeleteDisplacedSlotsAsync(string userId, long channelId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        var deleted = await _db.ProgramSlots
            .Where(s => s.TVChannelId == channelId && s.Status == ProgramSlotStatus.Displaced)
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation("[ProgramSchedule] {Deleted} verdrängte Sendeplaetze bereinigt.", deleted);
        return deleted;
    }

    /// <inheritdoc />
    public async Task<ProgramScheduleResetResult> ResetScheduleAsync(string userId, long channelId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        var slots = await _db.ProgramSlots
            .Where(s => s.TVChannelId == channelId)
            .ExecuteDeleteAsync(cancellationToken);
        var ruleIds = await _db.ProgramRules
            .Where(r => r.TVChannelId == channelId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        var cursors = await _db.ProgramRuleCursors
            .Where(c => ruleIds.Contains(c.ProgramRuleId))
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogWarning("[ProgramSchedule] Sendeplan zurueckgesetzt: {Slots} Sendeplaetze, {Cursors} Fortsetzungsstaende geloescht.", slots, cursors);
        return new ProgramScheduleResetResult(slots, cursors);
    }

    private async Task RequireAdminAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Sie sind nicht angemeldet.");

        if (!user.IsAdmin)
            throw new ForbiddenAccessException("Nur Administratoren dürfen die Planungsdiagnose abrufen.");
    }

    // ---- reconcile internals ----

    // Groups ordered slots into airings: maximal runs of the same producing rule where each slot
    // starts exactly at the previous slot's end.
    private static List<List<ProgramSlot>> GroupAirings(List<ProgramSlot> orderedSlots)
    {
        var airings = new List<List<ProgramSlot>>();
        foreach (var slot in orderedSlots)
        {
            var last = airings.Count > 0 ? airings[^1] : null;
            if (last is not null && last[^1].ProgramRuleId == slot.ProgramRuleId && last[^1].End == slot.Start)
            {
                last.Add(slot);
            }
            else
            {
                airings.Add([slot]);
            }
        }
        return airings;
    }

    // An airing is still valid iff its producing rule would schedule exactly this slot sequence for
    // the occurrence the airing belongs to. That occurrence may already have been in progress when
    // the plan was extended: the materialization writes only the slots ending after the coverage
    // start, so the first written slot of a partially aired occurrence legitimately begins
    // mid-occurrence instead of at the occurrence start (the already "aired" head items consumed
    // cursor progress but produced no slot row). A begun predecessor may additionally overshoot
    // into the occurrence start and shift the airing's real start behind its own — the check
    // therefore accepts any nominal occurrence start at or before the reconstructed real start.
    // It walks the item ring backwards from the airing's head item — each step counts one more
    // skipped head item — reconstructs the airing's real start as the airing's first slot start
    // minus the cumulative prefix durations, and re-runs the selection there with the boundary
    // of each candidate occurrence start. The written tail must then reproduce count, media
    // items and slot boundaries under the same selection logic (EpisodeCount / SlotDuration,
    // min-start-remainder and the overshoot boundary — a shifted or overshooting airing is
    // expected, not invalid).
    private static bool AiringReproducible(List<ProgramSlot> airing, RuleState state, HashSet<DateTime> occurrenceStarts, List<(DateTime StartUtc, int Priority)> boundStarts, Dictionary<DateTime, DateTime> invaderStarts, TimeSpan minStartRemain)
    {
        if (state.IsInterleaved)
            return AiringReproducibleInterleaved(airing, state, occurrenceStarts, boundStarts, invaderStarts, minStartRemain);

        var items = state.Items;
        if (items.Count == 0 || occurrenceStarts.Count == 0)
            return false;

        var idx = FindItemIndex(items, airing[0]);
        if (idx < 0)
            return false;

        var rule = state.Rule;
        var wanted = rule.EpisodeCount is > 0 ? rule.EpisodeCount.Value : int.MaxValue;

        var count = items.Count;
        var earliestStart = occurrenceStarts.Min();
        var skipped = TimeSpan.Zero;
        for (var k = 0; k < wanted; k++)
        {
            if (k > 0)
            {
                var headDuration = items[((idx - k) % count + count) % count].Duration;
                if (headDuration <= TimeSpan.Zero)
                    break; // degenerate duration — the backward walk would never progress
                skipped += headDuration;
            }
            // The airing's real start — its first item began here; the nominal occurrence
            // started at or before this point (the remainder was blocked by an overshooting
            // predecessor).
            var airingStart = airing[0].Start - skipped;
            if (airingStart < earliestStart)
                break; // skipped duration already reaches past the oldest occurrence in the window

            var headIndex = ((idx - k) % count + count) % count;
            foreach (var occurrenceStart in occurrenceStarts.Where(o => o <= airingStart))
            {
                // A shift needs invader evidence: an occupied slot covering the nominal start
                // whose end is exactly the real start — otherwise the earlier anchor would
                // rescue stale airings (e.g. after the rule's time-of-day changed).
                if (occurrenceStart < airingStart
                    && !(invaderStarts.TryGetValue(airingStart, out var invaderStart) && invaderStart < occurrenceStart))
                    continue;
                var expected = SelectAiringItems(items, headIndex, rule, airingStart,
                    BoundaryAfter(boundStarts, occurrenceStart, rule.Priority), minStartRemain);
                if (expected.Count - k != airing.Count)
                    continue;

                var start = airing[0].Start;
                var matches = true;
                for (var i = 0; i < airing.Count; i++)
                {
                    var slot = airing[i];
                    var item = expected[k + i];
                    if (slot.MediaItemId != item.MediaItemId || slot.Start != start || slot.End != start + item.Duration)
                    {
                        matches = false;
                        break;
                    }
                    start = slot.End;
                }
                if (matches)
                    return true;
            }
        }
        return false;
    }

    // Interleaved counterpart of <see cref="AiringReproducible"/> for multi-strand states: the
    // occurrence's picks rotate over the strands, so the check hypothesizes the number p of
    // picks before the airing's first slot (pick q lands on strand (startOffset + q) mod m —
    // hence that fixes the first slot's strand), reconstructs each strand's head as its first
    // airing index minus its prefix pick count, and verifies by simulating the occurrence at
    // the reconstructed real start under the boundary of each candidate nominal start (a begun
    // predecessor may have shifted the airing behind the nominal start). Strands without a
    // contribution to the airing cannot have supplied prefix picks (their head is not
    // observable) — such hypotheses are rejected. Conservative by design: an unclear prefix
    // fails the check, so the airing is displaced and re-materialized rather than kept stale.
    private static bool AiringReproducibleInterleaved(List<ProgramSlot> airing, RuleState state, HashSet<DateTime> occurrenceStarts, List<(DateTime StartUtc, int Priority)> boundStarts, Dictionary<DateTime, DateTime> invaderStarts, TimeSpan minStartRemain)
    {
        var strands = state.Strands;
        var m = strands.Count;
        var rule = state.Rule;
        if (occurrenceStarts.Count == 0)
            return false;

        // Locate each slot inside its strand; a strand's slots must be consecutive ring entries
        // in order (episodes of a series never air shuffled).
        var strandOf = new int[airing.Count];
        var firstIndex = new int[m];
        var countInAiring = new int[m];
        Array.Fill(firstIndex, -1);
        for (var i = 0; i < airing.Count; i++)
        {
            var s = -1;
            var idx = -1;
            for (var t = 0; t < m; t++)
            {
                idx = FindItemIndex(strands[t].Items, airing[i]);
                if (idx >= 0)
                {
                    s = t;
                    break;
                }
            }
            if (s < 0)
                return false;

            strandOf[i] = s;
            if (firstIndex[s] < 0)
            {
                firstIndex[s] = idx;
            }
            else if (idx != (firstIndex[s] + countInAiring[s]) % strands[s].Items.Count)
            {
                return false;
            }
            countInAiring[s]++;
        }

        var wanted = rule.EpisodeCount is > 0 ? rule.EpisodeCount.Value : int.MaxValue;
        var maxPrefix = Math.Min(wanted, strands.Sum(s => s.Items.Count) + airing.Count);

        foreach (var occurrenceStart in occurrenceStarts.Where(o => o <= airing[0].Start).OrderBy(o => o))
        {
            // The real start may shift across a UTC day boundary when invaders push it —
            // the materialization derives the start-strand offset from that shifted day,
            // so both plausible days are tried and checked for consistency below.
            var realDay = airing[0].Start.Date;
            var offsetCandidates = new[] { realDay, realDay.AddDays(-1) }
                .Select(d => StrandStartOffset(wanted, m, d))
                .Distinct();

            foreach (var startOffset in offsetCandidates)
            {
                for (var p = 0; p < maxPrefix; p++)
                {
                    if ((startOffset + p) % m != strandOf[0])
                        continue;

                    var prefixCount = new int[m];
                    var possible = true;
                    for (var q = 0; q < p && possible; q++)
                    {
                        var s = (startOffset + q) % m;
                        if (countInAiring[s] == 0)
                            possible = false;
                        else
                            prefixCount[s]++;
                    }
                    if (!possible)
                        break;

                    var simStrands = new List<StrandState>(m);
                    for (var s = 0; s < m; s++)
                    {
                        var items = strands[s].Items;
                        var head = countInAiring[s] > 0
                            ? ((firstIndex[s] - prefixCount[s]) % items.Count + items.Count) % items.Count
                            : 0;
                        simStrands.Add(new StrandState(strands[s].Key, items, head));
                    }

                    // The airing's real start: the p prefix picks occupy contiguous time ending at
                    // the first written slot — walk their durations back from airing[0].Start.
                    var simStart = airing[0].Start;
                    var localPick = new int[m];
                    for (var q = 0; q < p; q++)
                    {
                        var s = (startOffset + q) % m;
                        var items = strands[s].Items;
                        var head = simStrands[s].NextIndex;
                        simStart -= items[(head + localPick[s]) % items.Count].Duration;
                        localPick[s]++;
                    }

                    // The materialization derived the start-strand offset from the real start's
                    // day — a hypothesis is only consistent when it yields the same offset.
                    if (StrandStartOffset(wanted, m, simStart) != startOffset)
                        continue;

                    if (occurrenceStart > simStart
                        || (occurrenceStart < simStart
                            && !(invaderStarts.TryGetValue(simStart, out var inv) && inv < occurrenceStart)))
                        continue; // shift requires an occupied slot covering the nominal start

                    var picks = SelectAiringItemsInterleaved(simStrands, rule, simStart,
                        BoundaryAfter(boundStarts, occurrenceStart, rule.Priority), minStartRemain);
                    if (picks.Count - p != airing.Count)
                        continue;

                    var start = simStart;
                    var matches = true;
                    var pos = 0;
                    foreach (var pk in picks)
                    {
                        if (pos >= p)
                        {
                            var slot = airing[pos - p];
                            if (slot.MediaItemId != pk.Item.MediaItemId || slot.Start != start || slot.End != start + pk.Item.Duration)
                            {
                                matches = false;
                                break;
                            }
                        }
                        start += pk.Item.Duration;
                        pos++;
                    }
                    if (matches && pos == picks.Count)
                        return true;
                }
            }
        }
        return false;
    }

    // Locates a slot's content inside the expanded list: by media item first (unique), then by the
    // content reference (movie / episode) for material whose media item changed.
    private static int FindItemIndex(List<SchedulableItem> items, ProgramSlot slot)
    {
        var idx = items.FindIndex(i => i.MediaItemId == slot.MediaItemId);
        if (idx >= 0)
            return idx;
        if (slot.MovieId is { } movieId)
            return items.FindIndex(i => i.ContentType == ProgramCursorContentType.Movie && i.ContentId == movieId);
        if (slot.TVShowEpisodeId is { } episodeId)
            return items.FindIndex(i => i.ContentType == ProgramCursorContentType.TVShowEpisode && i.ContentId == episodeId);
        return -1;
    }

    // Rewinds each strand's cursor to the predecessor of its earliest displaced content that is
    // still expandable, so the next occurrence re-airs it (air order without a jump). Only
    // rewinds when the strand cursor has already passed that position.
    private static void RewindCursor(RuleState state, List<ProgramSlot> displacedSlots, IReadOnlyList<ProgramRuleCursor> cursorRows)
    {
        foreach (var strand in state.Strands)
        {
            var items = strand.Items;
            if (items.Count == 0)
                continue;

            foreach (var slot in displacedSlots.OrderBy(s => s.Start))
            {
                var idx = FindItemIndex(items, slot);
                if (idx < 0)
                    continue;

                if (strand.NextIndex > idx)
                {
                    var predecessor = items[(idx - 1 + items.Count) % items.Count];
                    var cursor = SelectStrandCursor(cursorRows, strand.Key);
                    if (cursor is not null)
                    {
                        cursor.LastContentType = predecessor.ContentType;
                        cursor.LastContentId = predecessor.ContentId;
                        cursor.UpdatedAt = DateTime.UtcNow;
                    }
                    strand.RewindTo(idx);
                }
                break; // earliest resolvable displaced content of this strand decides — done either way
            }
        }
    }

    // Like <see cref="MaterializeOccurrence"/>, but instead of dropping the airing on a conflict
    // with kept scheduled slots it returns the conflicting kept slots that lose against this
    // occurrence — the caller displaces them (whole rule suffix for competitors inside this
    // occurrence's window) and the materialization proceeds. Kept slots reaching across the
    // occurrence start are a begun airing's legitimate tail: they are never cut — the airing
    // simply starts behind their end.
    private List<ProgramSlot>? MaterializeOccurrenceEvicting(
        Occurrence occurrence,
        Dictionary<long, RuleState> states,
        List<ProgramSlot> keptSlots,
        List<Interval> blocked,
        List<ProgramSlot> newSlots,
        DateTime coverageStart,
        (DateTime StartUtc, bool Hard)? bound,
        SummaryCounters summary,
        Func<ProgramRule, ProgramSlot, bool> evicts)
    {
        var state = states[occurrence.Rule.Id];
        if (state.Items.Count == 0)
            return null;

        var airingStart = ShiftPastInvaders(occurrence.StartUtc, blocked);

        var (taken, takenCounts) = SelectItems(state, occurrence.Rule, airingStart, bound);
        if (taken.Count == 0)
            return null;

        var start = airingStart;
        var ranges = new List<Interval>();
        foreach (var item in taken)
        {
            var end = start + item.Duration;
            if (end > coverageStart)
                ranges.Add(new Interval(start, end));
            start = end;
        }

        // Fully-past occurrences produce nothing and must not consume the cursor — the original
        // materialization already did that.
        if (ranges.Count == 0)
            return null;

        var conflicts = keptSlots
            .Where(k => ranges.Any(r => r.Overlaps(new Interval(k.Start, k.End))))
            .ToList();
        // Slots starting before this occurrence's start are an earlier airing's overshooting tail
        // invading this window — they lose regardless of priority and are returned as losers
        // (the caller displaces their airing's future). Only slots competing head-on inside the
        // window go through the evicts check.
        var competitors = conflicts.Where(k => k.Start >= occurrence.StartUtc).ToList();
        if (competitors.Any(k => !evicts(occurrence.Rule, k)) || ranges.Any(r => newSlots.Select(s => new Interval(s.Start, s.End)).Any(r.Overlaps)))
        {
            summary.DroppedConflicts++;
            return null;
        }

        foreach (var (item, range) in taken.Skip(taken.Count - ranges.Count).Zip(ranges))
        {
            newSlots.Add(new ProgramSlot
            {
                TVChannelId = occurrence.Rule.TVChannelId,
                Start = range.Start,
                End = range.End,
                MediaItemId = item.MediaItemId,
                MovieId = item.MovieId,
                TVShowEpisodeId = item.TVShowEpisodeId,
                ProgramRuleId = occurrence.Rule.Id,
                RuleType = occurrence.Rule.RuleType,
                CreatedAt = DateTime.UtcNow
            });
        }

        blocked.AddRange(ranges);
        CommitTaken(state, taken, takenCounts);
        summary.SlotsCreated += ranges.Count;
        return conflicts.Count > 0 ? conflicts : null;
    }

    // ---- internal types ----

    private sealed record SchedulableItem(
        ProgramCursorContentType ContentType,
        long ContentId,
        long MediaItemId,
        long? MovieId,
        long? TVShowEpisodeId,
        TimeSpan Duration,
        string StrandKey = "");

    // Strand key of the shared strand holding all non-episode content of a genre expansion
    // (movies rotate through it like a series of their own — sequential, not looping daily).
    private const string OtherStrandKey = "other";

    // One ring per strand ("Sendestrang") of a rule's material: for interleaved genre rules
    // each TV show is its own strand (episodes continue in order independently of the other
    // shows), with a dedicated persisted cursor row; everything else contributes to the
    // shared "other" strand. Non-interleaved rules hold exactly one strand (Key == null).
    private sealed class StrandState(string? key, List<SchedulableItem> items, int nextIndex)
    {
        public string? Key { get; } = key;
        public List<SchedulableItem> Items { get; } = items;
        public int NextIndex { get; private set; } = nextIndex;
        public SchedulableItem? LastPlaced { get; private set; }

        public SchedulableItem? PeekAt(int offset)
            => Items.Count == 0 ? null : Items[(NextIndex + offset) % Items.Count];

        public void Commit(int count)
        {
            for (var i = 0; i < count; i++)
                LastPlaced = Items[(NextIndex + i) % Items.Count];
            NextIndex += count;
        }

        public void RewindTo(int index) => NextIndex = index;
    }

    private sealed class RuleState
    {
        public RuleState(ProgramRule rule, List<SchedulableItem> items, IReadOnlyList<ProgramRuleCursor> cursors)
        {
            Rule = rule;
            SetItems(items, cursors);
        }

        public ProgramRule Rule { get; }
        public List<SchedulableItem> Items { get; private set; } = [];
        public List<StrandState> Strands { get; private set; } = [];
        public bool IsInterleaved => Strands.Count > 1;

        // Flat-mode surface (single strand): used by filler handling and the flat airing
        // reproducibility check — identical semantics to the former single ring.
        public int NextIndex => Strands[0].NextIndex;
        public SchedulableItem? LastPlaced => Strands[0].LastPlaced;
        public SchedulableItem? PeekAt(int offset) => Strands[0].PeekAt(offset);
        public void Commit(int count) => Strands[0].Commit(count);
        public void RewindTo(int index) => Strands[0].RewindTo(index);

        // Rebuilds items and strands — used when a rule's expansion refreshes mid-run
        // (trailer material expands only after the occurrences are materialized).
        public void SetItems(List<SchedulableItem> items, IReadOnlyList<ProgramRuleCursor> cursors)
        {
            Items = items;

            if (Rule.MaterialType == ProgramRuleMaterialType.Genre && Rule.RuleType != ProgramRuleType.Filler)
            {
                var order = new List<string>();
                var byKey = new Dictionary<string, List<SchedulableItem>>();
                foreach (var item in items)
                {
                    var key = string.IsNullOrEmpty(item.StrandKey) ? OtherStrandKey : item.StrandKey;
                    if (!byKey.TryGetValue(key, out var list))
                    {
                        list = [];
                        byKey[key] = list;
                        order.Add(key);
                    }
                    list.Add(item);
                }
                if (order.Count > 1)
                {
                    // Deterministischer Hash-Mix statt Erstauftrittsreihenfolge: die Expansion
                    // liefert Serien alphabetisch sortiert ("A" zuerst) — mit dieser Reihenfolge
                    // belegten fruehe Strang-Positionen zuverlaessig die alphabetisch ersten
                    // Serien. Der Hash-Mix nach (Regel, Strang) ist reproduzierbar, variiert
                    // aber von Regel zu Regel; Cursors und Slot-Positionen bleiben stabil.
                    Strands = order
                        .OrderBy(key => StrandOrderKey(Rule.Id, key))
                        .Select(key => new StrandState(key, byKey[key], NextIndexFor(byKey[key], FindCursor(cursors, key))))
                        .ToList();
                    return;
                }
            }

            Strands = [new StrandState(null, items, NextIndexFor(items, FindCursor(cursors, null)))];
        }

        private static ProgramRuleCursor? FindCursor(IReadOnlyList<ProgramRuleCursor> cursors, string? key)
            => SelectStrandCursor(cursors, key);
    }

    private sealed record Occurrence(ProgramRule Rule, DateTime StartUtc);

    private readonly record struct Interval(DateTime Start, DateTime End)
    {
        public bool Overlaps(Interval other) => Start < other.End && End > other.Start;
    }

    // Advances a would-be occurrence start past every occupied interval covering it: a slot
    // reaching across the start is a begun airing's overshooting tail — it always airs to the
    // end, so the new airing takes its start behind the tail's real end instead of displacing
    // the video mid-air or dropping the whole airing.
    // Stabiler, prozess-invarianter Ordnungsschluessel je (Regel, Strang): FNV-1a ueber
    // Regel-Id und Strang-Schluessel. `string.GetHashCode` waere prozess-zufaellig und
    // wuerde die Strangreihenfolge von Lauf zu Lauf veraendern — die Mischung muss
    // reproduzierbar bleiben, damit Plaene und Cursor stabil sind.
    internal static int StrandOrderKey(long ruleId, string strandKey)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes($"{ruleId}:{strandKey}"))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return (int)(hash & 0x7fffffff);
        }
    }

    // Deterministic winner pick among a rule's cursor rows of one strand: normally the
    // filtered unique index guarantees a single row, but defence in depth (duplicates could
    // still arrive via an unchecked path) picks the same winner the data migration and the
    // restore deduplication use — highest UpdatedAt, ties broken by highest Id.
    internal static ProgramRuleCursor? SelectStrandCursor(IReadOnlyList<ProgramRuleCursor> cursors, string? key)
        => cursors
            .Where(c => c.StrandKey == key)
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefault();

    // The starting strand of an interleaved airing derives from the real (possibly shifted)
    // start day — EpisodeCount-only rules rotate it per day so every strand gets its turn;
    // SlotDuration airings keep identical day-to-day positions (offset 0).
    private static int StrandStartOffset(int wanted, int strandCount, DateTime startUtc)
        => wanted < strandCount
            ? (int)((((startUtc.Date - DateTime.UnixEpoch).Days) % strandCount) + strandCount) % strandCount
            : 0;

    private static DateTime ShiftPastInvaders(DateTime startUtc, IEnumerable<Interval> occupied)
    {
        var moved = true;
        while (moved)
        {
            moved = false;
            foreach (var i in occupied)
            {
                if (i.Start < startUtc && i.End > startUtc)
                {
                    startUtc = i.End;
                    moved = true;
                }
            }
        }
        return startUtc;
    }

    private sealed class SummaryCounters
    {
        public int SlotsCreated;
        public int FillerSlotsCreated;
        public int SkippedNoDuration;
        public int SkippedNoAccess;
        public int OrphanedRules;
        public int DroppedConflicts;
        public long RemainingGapSeconds;

        // Diagnosis detail counters — only consumed by GetRuleDiagnosesAsync; the public run
        // and reconcile summaries are unchanged.
        public int Candidates;
        public int MaterialMissing;
        public int NoMediaItem;
        public int ProbePending;
        public int ProbeExhausted;

        public ProgramScheduleRunSummary ToSummary()
            => new(SlotsCreated, FillerSlotsCreated, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds);
    }

    // The audience-access snapshot: a content is schedulable when every non-admin user can see it —
    // either via regular media-source access or an individual unlock of its unlock root (the owning
    // TV show or movie collection). With no non-admin users everything is released.
    private sealed class AccessContext
    {
        public bool Everything { get; init; }
        public required HashSet<long> FullySharedSourceIds { get; init; }
        public required HashSet<long> UnlockedShowIdsForAll { get; init; }
        public required HashSet<long> UnlockedCollectionIdsForAll { get; init; }

        public bool Allows(long sourceId, long? showId, long? collectionId)
            => Everything
                || FullySharedSourceIds.Contains(sourceId)
                || (showId is { } s && UnlockedShowIdsForAll.Contains(s))
                || (collectionId is { } c && UnlockedCollectionIdsForAll.Contains(c));
    }

    private async Task<AccessContext> LoadAccessContextAsync(CancellationToken cancellationToken)
    {
        var userIds = await _db.Users.Where(u => !u.IsAdmin).Select(u => u.Id).ToListAsync(cancellationToken);
        if (userIds.Count == 0)
        {
            return new AccessContext
            {
                Everything = true,
                FullySharedSourceIds = [],
                UnlockedShowIdsForAll = [],
                UnlockedCollectionIdsForAll = []
            };
        }

        var audienceCount = userIds.Count;

        var fullyShared = (await _db.MediaSourceUsers
                .Where(m => userIds.Contains(m.UserId))
                .Select(m => new { m.MediaSourceId, m.UserId })
                .ToListAsync(cancellationToken))
            .GroupBy(m => m.MediaSourceId)
            .Where(g => g.Select(m => m.UserId).Distinct().Count() == audienceCount)
            .Select(g => g.Key)
            .ToHashSet();

        var unlocks = await _db.UnlockedMediaEntries
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.TVShowId, u.MovieCollectionId })
            .ToListAsync(cancellationToken);

        var unlockedShows = unlocks
            .Where(u => u.TVShowId is not null)
            .GroupBy(u => u.TVShowId!.Value)
            .Where(g => g.Select(u => u.UserId).Distinct().Count() == audienceCount)
            .Select(g => g.Key)
            .ToHashSet();

        var unlockedCollections = unlocks
            .Where(u => u.MovieCollectionId is not null)
            .GroupBy(u => u.MovieCollectionId!.Value)
            .Where(g => g.Select(u => u.UserId).Distinct().Count() == audienceCount)
            .Select(g => g.Key)
            .ToHashSet();

        return new AccessContext
        {
            Everything = false,
            FullySharedSourceIds = fullyShared,
            UnlockedShowIdsForAll = unlockedShows,
            UnlockedCollectionIdsForAll = unlockedCollections
        };
    }

    // Index of the element right after the cursor position; 0 when there is no cursor or the
    // cursor element no longer exists in the expanded list.
    private static int NextIndexFor(List<SchedulableItem> items, ProgramRuleCursor? cursor)
    {
        if (cursor is null || items.Count == 0)
            return 0;

        var pos = items.FindIndex(i => i.ContentType == cursor.LastContentType && i.ContentId == cursor.LastContentId);
        return pos < 0 ? 0 : pos + 1;
    }

    private List<Occurrence> BuildOccurrences(IEnumerable<ProgramRule> rules, DateTime coverageStartUtc, DateTimeOffset horizonEnd, string? federalState)
    {
        var tz = TimeZoneInfo.Local;
        var windowStartDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(coverageStartUtc, tz)).AddDays(-1);
        var windowEndDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(horizonEnd.UtcDateTime, tz));

        var occurrences = new List<Occurrence>();
        foreach (var rule in rules)
        {
            for (var day = windowStartDay; day <= windowEndDay; day = day.AddDays(1))
            {
                var applies = rule.Recurrence switch
                {
                    ProgramRuleRecurrence.Daily => true,
                    ProgramRuleRecurrence.Weekly => rule.DayOfWeek is { } dow && day.DayOfWeek == dow,
                    ProgramRuleRecurrence.Once => rule.SingleDate == day,
                    ProgramRuleRecurrence.Holiday => GermanHolidayCalendar.IsHoliday(day, federalState),
                    ProgramRuleRecurrence.Weekday => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
                    ProgramRuleRecurrence.Weekend => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                    _ => false
                };
                if (!applies)
                    continue;

                var localStart = day.ToDateTime(rule.TimeOfDay, DateTimeKind.Unspecified);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
                if (startUtc >= coverageStartUtc - OccurrenceLookback && startUtc < horizonEnd.UtcDateTime)
                    occurrences.Add(new Occurrence(rule, startUtc));
            }
        }

        return occurrences;
    }

    private void MaterializeOccurrence(Occurrence occurrence, Dictionary<long, RuleState> states, List<Interval> blocked, List<ProgramSlot> newSlots, DateTime coverageStart, (DateTime StartUtc, bool Hard)? bound, SummaryCounters summary)
    {
        var state = states[occurrence.Rule.Id];
        if (state.Items.Count == 0)
            return;

        // A slot already occupying the occurrence start is a begun airing's overshooting tail —
        // it always airs to the end, so this airing starts behind it ("verschiebt sich").
        var airingStart = ShiftPastInvaders(occurrence.StartUtc, blocked);

        var (taken, takenCounts) = SelectItems(state, occurrence.Rule, airingStart, bound);
        if (taken.Count == 0)
            return;

        // Slots that ended before the coverage start are history — the items still consume cursor
        // progress (they "aired"), but no slot row is written. Slots spanning the coverage start
        // keep their true Start.
        var start = airingStart;
        var ranges = new List<Interval>();
        foreach (var item in taken)
        {
            var end = start + item.Duration;
            if (end > coverageStart)
                ranges.Add(new Interval(start, end));
            start = end;
        }

        // An airing is atomic: if any of its slots collides with already occupied ranges
        // (persisted coverage or a higher-priority airing), the whole airing is dropped and the
        // cursor is not advanced.
        if (ranges.Any(r => blocked.Any(b => b.Overlaps(r)) || newSlots.Select(s => new Interval(s.Start, s.End)).Any(r.Overlaps)))
        {
            summary.DroppedConflicts++;
            return;
        }

        foreach (var (item, range) in taken.Skip(taken.Count - ranges.Count).Zip(ranges))
        {
            newSlots.Add(new ProgramSlot
            {
                TVChannelId = occurrence.Rule.TVChannelId,
                Start = range.Start,
                End = range.End,
                MediaItemId = item.MediaItemId,
                MovieId = item.MovieId,
                TVShowEpisodeId = item.TVShowEpisodeId,
                ProgramRuleId = occurrence.Rule.Id,
                RuleType = occurrence.Rule.RuleType,
                CreatedAt = DateTime.UtcNow
            });
        }

        blocked.AddRange(ranges);
        CommitTaken(state, taken, takenCounts);
        summary.SlotsCreated += ranges.Count;
    }

    // Shared selection entry for both materialization paths: picks the airing's items without
    // committing them — the taken items are only committed once the airing survives the
    // conflict check (see <see cref="CommitTaken"/>). For flat (single-strand) states this is
    // exactly the ring behavior; for interleaved states the window is filled round-robin over
    // the strands (<see cref="SelectAiringItemsInterleaved"/>) and the per-strand take counts
    // come back in the second tuple element (null when flat). <paramref name="airingStartUtc"/>
    // is the airing's real start — the occurrence start, or the end of an overshooting
    // predecessor when the window begins occupied.
    private (List<SchedulableItem> Taken, int[]? TakenCounts) SelectItems(
        RuleState state, ProgramRule rule, DateTime airingStartUtc, (DateTime StartUtc, bool Hard)? bound)
    {
        if (!state.IsInterleaved)
        {
            var taken = SelectAiringItems(state.Items, state.NextIndex, rule, airingStartUtc, bound, _minStartRemain);
            return (taken, null);
        }

        var picks = SelectAiringItemsInterleaved(state.Strands, rule, airingStartUtc, bound, _minStartRemain);
        var takenCounts = new int[state.Strands.Count];
        var selected = new List<SchedulableItem>();
        foreach (var (strand, item) in picks)
        {
            takenCounts[strand]++;
            selected.Add(item);
        }
        return (selected, takenCounts);
    }

    // Commits the taken items of a selection after the airing passed the conflict check.
    private static void CommitTaken(RuleState state, List<SchedulableItem> taken, int[]? takenCounts)
    {
        if (takenCounts is not null)
        {
            for (var i = 0; i < state.Strands.Count; i++)
                state.Strands[i].Commit(takenCounts[i]);
        }
        else
        {
            state.Commit(taken.Count);
        }
    }

    // Interleaved variant of <see cref="SelectAiringItems"/> for multi-strand states: the
    // window is filled round-robin over the strands in fixed order, each occurrence starting
    // again at the first strand — the series keep their slot position across days while each
    // strand's own cursor advances in episode order. The same boundary rule as the flat variant
    // applies per strand: a started item always completes (overshoot allowed against soft
    // bounds); a strand stops contributing when its head may no longer start (or cannot
    // complete before a hard bound) — the item defers, it is never skipped or cut.
    // Returns the pick sequence: (strand index, item).
    private static List<(int Strand, SchedulableItem Item)> SelectAiringItemsInterleaved(
        List<StrandState> strands, ProgramRule rule, DateTime airingStartUtc,
        (DateTime StartUtc, bool Hard)? bound, TimeSpan minStartRemain)
    {
        var wanted = rule.EpisodeCount is > 0 ? rule.EpisodeCount.Value
            : rule.SlotDuration is not null ? int.MaxValue : 1;
        var target = rule.EpisodeCount is > 0 ? (TimeSpan?)null : rule.SlotDuration;

        var m = strands.Count;
        var picks = new List<(int Strand, SchedulableItem Item)>();
        var taken = new int[m];
        var done = new bool[m];
        var slotStart = airingStartUtc;
        var accumulated = TimeSpan.Zero;
        var selected = 0;

        // EpisodeCount < strand count would otherwise serve only strand 0 forever;
        // rotating the start strand by occurrence day gives every strand its turn
        // over consecutive days. With SlotDuration (wanted >= m) positions stay
        // identical day-to-day, so the offset stays 0.
        var startOffset = StrandStartOffset(wanted, m, airingStartUtc);

        var maxLap = strands.Max(s => s.Items.Count);
        var noProgressLaps = 0;
        while (selected < wanted)
        {
            var progressed = false;
            var lapAccumulated = accumulated;
            for (var i = 0; i < m && selected < wanted; i++)
            {
                var s = (startOffset + i) % m;
                var strand = strands[s];
                if (done[s] || strand.Items.Count == 0)
                    continue;
                var item = strand.Items[(strand.NextIndex + taken[s]) % strand.Items.Count];
                if (bound is { } b && (b.StartUtc - slotStart < minStartRemain || (b.Hard && slotStart + item.Duration > b.StartUtc)))
                {
                    done[s] = true; // head may not start / cannot complete — the strand rests
                    continue;
                }
                picks.Add((s, item));
                taken[s]++;
                selected++;
                slotStart += item.Duration;
                accumulated += item.Duration;
                progressed = true;
                if (target is not null && accumulated >= target)
                    break;
            }
            if (!progressed)
                break;
            noProgressLaps = accumulated > lapAccumulated ? 0 : noProgressLaps + 1;
            if (noProgressLaps > maxLap)
                break; // every strand cycled a full lap without gaining airtime — degenerate durations
        }
        return picks;
    }

    // Boundary ownership: every occurrence owns [StartUtc, next occurrence start of any
    // item-bearing rule). An item is placed whenever it can still START inside the window
    // (<paramref name="minStartRemain"/> before the boundary) — and it always airs to the end:
    // items are indivisible, so a started item may legitimately overshoot a soft boundary and
    // shifts the following airing behind its real end. Against a hard boundary (higher-priority
    // next occurrence) an item must additionally complete inside the window. An item that may
    // not start simply waits — it is never cut and never silently skipped: the cursor stays and
    // the next occurrence retries it.
    private static List<SchedulableItem> SelectAiringItems(
        List<SchedulableItem> items, int headIndex, ProgramRule rule, DateTime airingStartUtc,
        (DateTime StartUtc, bool Hard)? bound, TimeSpan minStartRemain)
    {
        // EpisodeCount wins; without it SlotDuration bounds the airing; when neither is set the
        // airing gets exactly one item (rule validation normally requires one of both).
        var wanted = rule.EpisodeCount is > 0 ? rule.EpisodeCount.Value
            : rule.SlotDuration is not null ? int.MaxValue : 1;
        var target = rule.EpisodeCount is > 0 ? (TimeSpan?)null : rule.SlotDuration;

        var count = items.Count;
        var selected = new List<SchedulableItem>();
        var accumulated = TimeSpan.Zero;
        var slotStart = airingStartUtc;
        var ring = 0;
        while (selected.Count < wanted)
        {
            var item = items[(headIndex + ring) % count];
            if (bound is { } b)
            {
                if (b.StartUtc - slotStart < minStartRemain)
                    break; // not enough room left to start — the rest goes to fillers
                if (b.Hard && slotStart + item.Duration > b.StartUtc)
                    break; // higher-priority window — the item cannot complete, defers
            }

            selected.Add(item);
            slotStart += item.Duration;
            accumulated += item.Duration;
            if (target is not null && accumulated >= target)
                break;
            if (++ring >= count && accumulated <= TimeSpan.Zero)
                break; // a full ring gained no airtime — degenerate (zero) durations
        }
        return selected;
    }

    // Sorted start times of all occurrences whose rule currently expands to schedulable items —
    // every such start bounds the airing before it: a begun item always airs to the end, so the
    // boundary governs whether an item may still START, not how long it may run.
    private static List<(DateTime StartUtc, int Priority)> BoundStarts(List<Occurrence> occurrences, Dictionary<long, RuleState> states)
        => occurrences
            .Where(o => states[o.Rule.Id].Items.Count > 0)
            .Select(o => (o.StartUtc, o.Rule.Priority))
            .Distinct()
            .OrderBy(s => s.StartUtc)
            .ToList();

    // First bound occurrence start strictly after <paramref name="afterUtc"/> — the end of the
    // window the airing owns. Two flavors: a soft bound (equal-or-lower priority) only gates
    // whether an item may still start — a started item overshoots and completes, shifting the
    // next airing behind it. A hard bound (strictly higher priority) additionally requires the
    // item to END before it — that window can never be won, so an item that would not complete
    // waits for the rule's next occurrence instead of being scheduled and killed.
    private static (DateTime StartUtc, bool Hard)? BoundaryAfter(List<(DateTime StartUtc, int Priority)> boundStarts, DateTime afterUtc, int priority)
    {
        foreach (var (start, p) in boundStarts)
            if (start > afterUtc)
                return (start, p > priority);
        return null;
    }

    private void FillGaps(Dictionary<long, RuleState> states, List<ProgramRule> rules, List<Interval> blocked, List<ProgramSlot> newSlots, DateTime coverageStart, DateTime horizonEnd, SummaryCounters summary,
        IReadOnlyDictionary<long, List<DateTime>>? airingsByMovie = null)
    {
        var fillerStates = rules
            .Where(r => r.RuleType == ProgramRuleType.Filler)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id)
            .Select(r => states[r.Id])
            .Where(s => s.Items.Count > 0)
            .ToList();
        if (fillerStates.Count == 0)
        {
            summary.RemainingGapSeconds += GapSeconds(blocked, coverageStart, horizonEnd);
            return;
        }

        var occupied = blocked.Concat(newSlots.Select(s => new Interval(s.Start, s.End)))
            .OrderBy(i => i.Start)
            .ToList();

        var gaps = new List<Interval>();
        var cursor = coverageStart;
        foreach (var interval in occupied)
        {
            if (interval.Start > cursor && cursor < horizonEnd)
                gaps.Add(new Interval(cursor, Math.Min(interval.Start.Ticks, horizonEnd.Ticks) == interval.Start.Ticks ? interval.Start : horizonEnd));
            if (interval.End > cursor)
                cursor = interval.End;
        }
        if (cursor < horizonEnd)
            gaps.Add(new Interval(cursor, horizonEnd));

        foreach (var gap in gaps)
        {
            var fillAt = gap.Start;
            var progressed = true;
            while (fillAt < gap.End && progressed)
            {
                progressed = false;
                foreach (var filler in fillerStates)
                {
                    if (fillAt >= gap.End)
                        break;
                    var take = 1;
                    SchedulableItem? item;
                    if (filler.Rule.MaterialType == ProgramRuleMaterialType.Trailer)
                    {
                        // Vorlauf: only a trailer whose movie airs within the lead window after
                        // this position qualifies — otherwise the rule contributes nothing here
                        // and the remaining filler rules cover the gap (fallback).
                        var offset = airingsByMovie is null
                            ? (int?)null
                            : FindLeadMatchOffset(filler, fillAt, fillAt + _trailerLead, airingsByMovie);
                        if (offset is null)
                            continue;
                        take = offset.Value + 1;
                        item = filler.PeekAt(offset.Value);
                    }
                    else
                    {
                        item = filler.PeekAt(0);
                    }
                    // Fillers must fit inside the gap — except at the horizon edge, where the last
                    // one may overshoot the window end so coverage truly reaches the horizon.
                    var overshootAllowed = gap.End >= horizonEnd;
                    if (item is null || (fillAt + item.Duration > gap.End && !overshootAllowed))
                        continue;

                    newSlots.Add(new ProgramSlot
                    {
                        TVChannelId = filler.Rule.TVChannelId,
                        Start = fillAt,
                        End = fillAt + item.Duration,
                        MediaItemId = item.MediaItemId,
                        MovieId = item.MovieId,
                        TVShowEpisodeId = item.TVShowEpisodeId,
                        ProgramRuleId = filler.Rule.Id,
                        RuleType = ProgramRuleType.Filler,
                        CreatedAt = DateTime.UtcNow
                    });
                    filler.Commit(take);
                    fillAt += item.Duration;
                    summary.FillerSlotsCreated++;
                    summary.SlotsCreated++;
                    progressed = true;
                }
            }

            if (fillAt < gap.End)
                summary.RemainingGapSeconds += (long)(gap.End - fillAt).TotalSeconds;
        }
    }

    private static long GapSeconds(List<Interval> occupied, DateTime coverageStart, DateTime horizonEnd)
    {
        var cursor = coverageStart;
        long seconds = 0;
        foreach (var interval in occupied.OrderBy(i => i.Start))
        {
            if (interval.Start > cursor && cursor < horizonEnd)
                seconds += (long)(new DateTime(Math.Min(interval.Start.Ticks, horizonEnd.Ticks)) - cursor).TotalSeconds;
            if (interval.End > cursor)
                cursor = interval.End;
        }
        if (cursor < horizonEnd)
            seconds += (long)(horizonEnd - cursor).TotalSeconds;
        return seconds;
    }

    // Writes one cursor row per strand that placed content this run: flat rules keep their
    // single NULL-StrandKey row, interleaved (genre) rules one row per series/other strand.
    private void PersistCursors(Dictionary<long, RuleState> states, Dictionary<long, IReadOnlyList<ProgramRuleCursor>> cursors)
    {
        var now = DateTime.UtcNow;
        foreach (var state in states.Values)
        {
            var rows = cursors.GetValueOrDefault(state.Rule.Id);
            foreach (var strand in state.Strands.Where(s => s.LastPlaced is not null))
            {
                var placed = strand.LastPlaced!;
                var cursor = rows is null ? null : SelectStrandCursor(rows, strand.Key);
                if (cursor is not null)
                {
                    cursor.LastContentType = placed.ContentType;
                    cursor.LastContentId = placed.ContentId;
                    cursor.UpdatedAt = now;
                }
                else
                {
                    _db.ProgramRuleCursors.Add(new ProgramRuleCursor
                    {
                        ProgramRuleId = state.Rule.Id,
                        StrandKey = strand.Key,
                        LastContentType = placed.ContentType,
                        LastContentId = placed.ContentId,
                        UpdatedAt = now
                    });
                }
            }
        }
    }

    // ---- material expansion ----

    private async Task<List<SchedulableItem>> ExpandMaterialAsync(ProgramRule rule, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
        => await ExpandMaterialAsync(rule, access, summary, null, DateTime.UtcNow, cancellationToken);

    private async Task<List<SchedulableItem>> ExpandMaterialAsync(ProgramRule rule, AccessContext access, SummaryCounters summary,
        IReadOnlyDictionary<long, List<DateTime>>? airingsByMovie, DateTime coverageStart, CancellationToken cancellationToken)
    {
        var items = rule.MaterialType switch
        {
            ProgramRuleMaterialType.TVShow => await ExpandShowAsync(rule.MaterialId, access, summary, cancellationToken),
            ProgramRuleMaterialType.Movie => await ExpandMovieAsync(rule.MaterialId, access, summary, cancellationToken),
            ProgramRuleMaterialType.MovieCollection => await ExpandMovieCollectionAsync(rule.MaterialId, access, summary, cancellationToken),
            ProgramRuleMaterialType.Genre => await ExpandGenreAsync(rule, access, summary, cancellationToken),
            ProgramRuleMaterialType.Playlist => await ExpandPlaylistAsync(rule.MaterialId, access, summary, cancellationToken),
            ProgramRuleMaterialType.Trailer => await ExpandTrailersAsync(access, airingsByMovie ?? new Dictionary<long, List<DateTime>>(), coverageStart, summary, cancellationToken),
            ProgramRuleMaterialType.MediaSourceClips => await ExpandMediaSourceClipsAsync(rule.MaterialId, access, summary, cancellationToken),
            _ => new List<SchedulableItem>()
        };

        if (items.Count == 0)
        {
            summary.OrphanedRules++;
            _logger.LogInformation(
                "[ProgramSchedule] Regel {RuleId} ({Name}) liefert kein planbares Material (verwaist, ohne Laufzeit oder nicht freigegeben).",
                rule.Id, rule.Name);
        }

        return items;
    }

    // One representative media-item link per content element, including its probe state so the
    // diagnosis can tell "no media file", "probe pending" and "probe exhausted" apart. Items with
    // a probed duration are preferred — ties keep the historical lowest-MediaItemId choice.
    private sealed record MediaItemRef(long MediaItemId, TimeSpan? Duration, DateTime? DurationProbedAt, int DurationProbeAttempts);

    private async Task<Dictionary<long, MediaItemRef>> MovieMediaItemsAsync(IReadOnlyCollection<long> movieIds, CancellationToken cancellationToken)
    {
        var links = await _db.MovieMediaItems
            .Where(l => movieIds.Contains(l.MovieId) && !l.IsTrailer)
            .Select(l => new { l.MovieId, l.MediaItemId, l.MediaItem.Duration, l.MediaItem.DurationProbedAt, l.MediaItem.DurationProbeAttempts })
            .ToListAsync(cancellationToken);
        return links.GroupBy(l => l.MovieId).ToDictionary(
            g => g.Key,
            g => g.OrderBy(l => l.Duration is null ? 1 : 0).ThenBy(l => l.MediaItemId)
                .Select(l => new MediaItemRef(l.MediaItemId, l.Duration, l.DurationProbedAt, l.DurationProbeAttempts))
                .First());
    }

    private async Task<Dictionary<long, MediaItemRef>> EpisodeMediaItemsAsync(IReadOnlyCollection<long> episodeIds, CancellationToken cancellationToken)
    {
        var links = await _db.TVShowEpisodeMediaItems
            .Where(l => episodeIds.Contains(l.TVShowEpisodeId))
            .Select(l => new { l.TVShowEpisodeId, l.MediaItemId, l.MediaItem.Duration, l.MediaItem.DurationProbedAt, l.MediaItem.DurationProbeAttempts })
            .ToListAsync(cancellationToken);
        return links.GroupBy(l => l.TVShowEpisodeId).ToDictionary(
            g => g.Key,
            g => g.OrderBy(l => l.Duration is null ? 1 : 0).ThenBy(l => l.MediaItemId)
                .Select(l => new MediaItemRef(l.MediaItemId, l.Duration, l.DurationProbedAt, l.DurationProbeAttempts))
                .First());
    }

    // Shared skip-reason bookkeeping for a candidate whose media item is missing or unprobed.
    // All three cases still count as SkippedNoDuration for the public run summary; the diagnosis
    // counters tell them apart.
    private void CountNoDuration(MediaItemRef? media, SummaryCounters summary)
    {
        summary.SkippedNoDuration++;
        if (media is null)
        {
            summary.NoMediaItem++;
        }
        else if (media.DurationProbedAt is not null && media.DurationProbeAttempts >= _maxProbeAttempts)
        {
            summary.ProbeExhausted++;
        }
        else
        {
            summary.ProbePending++;
        }
    }

    private SchedulableItem? TryMovieItem(Movie movie, Dictionary<long, MediaItemRef> mediaItems, AccessContext access, SummaryCounters summary)
    {
        summary.Candidates++;
        mediaItems.TryGetValue(movie.Id, out var media);
        if (media?.Duration is null)
        {
            CountNoDuration(media, summary);
            return null;
        }
        if (!access.Allows(movie.MediaSourceId, null, movie.MovieCollectionId))
        {
            summary.SkippedNoAccess++;
            return null;
        }
        return new SchedulableItem(ProgramCursorContentType.Movie, movie.Id, media.MediaItemId, movie.Id, null, media.Duration.Value);
    }

    private async Task<List<SchedulableItem>> ExpandShowAsync(long showId, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        if (!await _db.TVShows.AnyAsync(s => s.Id == showId, cancellationToken))
        {
            summary.MaterialMissing++;
            return [];
        }

        var episodes = await _db.TVShowEpisodes
            .Where(e => e.TVShowSeason.TVShowId == showId)
            .OrderBy(e => e.TVShowSeason.Id)
            .ThenBy(e => e.Number)
            .Select(e => new { e.Id, e.TVShowSeasonId, e.MediaSourceId })
            .ToListAsync(cancellationToken);

        var mediaItems = await EpisodeMediaItemsAsync(episodes.Select(e => e.Id).ToList(), cancellationToken);

        return episodes
            .Select(e => ToItem(new ProxyEpisode(e.Id, e.MediaSourceId), showId, mediaItems, access, summary))
            .Where(i => i is not null)
            .Cast<SchedulableItem>()
            .ToList();
    }

    // Minimal carrier for episode fields needed by ToItem (id + source).
    private sealed record ProxyEpisode(long Id, long MediaSourceId);

    private SchedulableItem? ToItem(ProxyEpisode episode, long showId, Dictionary<long, MediaItemRef> mediaItems, AccessContext access, SummaryCounters summary)
    {
        summary.Candidates++;
        mediaItems.TryGetValue(episode.Id, out var media);
        if (media?.Duration is null)
        {
            CountNoDuration(media, summary);
            return null;
        }
        if (!access.Allows(episode.MediaSourceId, showId, null))
        {
            summary.SkippedNoAccess++;
            return null;
        }
        return new SchedulableItem(ProgramCursorContentType.TVShowEpisode, episode.Id, media.MediaItemId, null, episode.Id, media.Duration.Value);
    }

    private async Task<List<SchedulableItem>> ExpandMovieAsync(long movieId, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        var movie = await _db.Movies.FirstOrDefaultAsync(m => m.Id == movieId, cancellationToken);
        if (movie is null)
        {
            summary.MaterialMissing++;
            return [];
        }

        var mediaItems = await MovieMediaItemsAsync([movie.Id], cancellationToken);
        var item = TryMovieItem(movie, mediaItems, access, summary);
        return item is null ? [] : [item];
    }

    private async Task<List<SchedulableItem>> ExpandMovieCollectionAsync(long collectionId, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        if (!await _db.MovieCollections.AnyAsync(c => c.Id == collectionId, cancellationToken))
        {
            summary.MaterialMissing++;
            return [];
        }

        var movies = await _db.Movies
            .Where(m => m.MovieCollectionId == collectionId)
            .OrderBy(m => m.ReleaseDate ?? m.PremieredAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var mediaItems = await MovieMediaItemsAsync(movies.Select(m => m.Id).ToList(), cancellationToken);
        return movies
            .Select(m => TryMovieItem(m, mediaItems, access, summary))
            .Where(i => i is not null)
            .Cast<SchedulableItem>()
            .ToList();
    }

    // Expands genre material to the deduplicated union over all selected genres. The stored genre
    // rows resolve to canonical names (alternate names forward — same source, recursive,
    // cycle-safe; hidden/out-of-season rows without alternates contribute nothing), and every
    // genre row in the in-scope media sources resolving to one of those names feeds its linked
    // movies and shows into the union — including legacy links on hidden genre rows that forward
    // to a selected canonical genre. The per-rule media-source filter additionally restricts both
    // the genre rows and the resulting content; empty filter = all sources. Rules without
    // ProgramRuleGenre rows (legacy data, old backups) fall back to MaterialId as the single
    // genre.
    private async Task<List<SchedulableItem>> ExpandGenreAsync(ProgramRule rule, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        var sourceFilter = rule.MediaSourceFilters.Select(s => s.MediaSourceId).ToHashSet();
        var storedIds = rule.Genres.Count > 0
            ? rule.Genres.Select(g => g.GenreId).ToList()
            : [rule.MaterialId];

        var scopedGenres = await _db.Genres
            .AsNoTracking()
            .Include(g => g.AlternateNames)
            .Where(g => sourceFilter.Count == 0 || sourceFilter.Contains(g.MediaSourceId))
            .ToListAsync(cancellationToken);

        var storedGenres = await _db.Genres
            .AsNoTracking()
            .Include(g => g.AlternateNames)
            .Where(g => storedIds.Contains(g.Id))
            .ToListAsync(cancellationToken);
        if (storedGenres.Count == 0)
        {
            summary.MaterialMissing++;
            return [];
        }

        var allRows = scopedGenres
            .Concat(storedGenres.Where(s => scopedGenres.All(g => g.Id != s.Id)))
            .ToList();
        var canonicalNames = GenreCanonicalResolver.ResolveCanonicalNames(allRows, DateTime.UtcNow);

        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stored in storedGenres)
            selectedNames.UnionWith(canonicalNames.GetValueOrDefault(stored.Id, []));
        if (selectedNames.Count == 0)
        {
            // Every stored genre is hidden/out-of-season without a usable alternate — the
            // selection resolves to nothing.
            summary.MaterialMissing++;
            return [];
        }

        var effectiveGenreIds = scopedGenres
            .Where(g => canonicalNames[g.Id].Overlaps(selectedNames))
            .Select(g => g.Id)
            .ToHashSet();
        if (effectiveGenreIds.Count == 0)
        {
            // The source filter excludes every genre row the selection resolves to — no material
            // can ever match in this configuration.
            summary.MaterialMissing++;
            return [];
        }

        var movieIds = await _db.MovieGenres
            .Where(g => effectiveGenreIds.Contains(g.GenreId))
            .Select(g => g.MovieId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var movies = await _db.Movies
            .Where(m => movieIds.Contains(m.Id))
            .Where(m => sourceFilter.Count == 0 || sourceFilter.Contains(m.MediaSourceId))
            .OrderBy(m => m.ReleaseDate ?? m.PremieredAt)
            .ThenBy(m => m.Name)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var showIds = await _db.TVShowGenres
            .Where(g => effectiveGenreIds.Contains(g.GenreId))
            .Select(g => g.TVShowId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var episodes = await _db.TVShowEpisodes
            .Where(e => showIds.Contains(e.TVShowSeason.TVShowId))
            .Where(e => sourceFilter.Count == 0 || sourceFilter.Contains(e.MediaSourceId))
            .OrderBy(e => e.TVShowSeason.TVShow.Name)
            .ThenBy(e => e.TVShowSeason.TVShowId)
            .ThenBy(e => e.TVShowSeason.Id)
            .ThenBy(e => e.Number)
            .Select(e => new { e.Id, e.MediaSourceId, ShowId = e.TVShowSeason.TVShowId })
            .ToListAsync(cancellationToken);

        var mediaByMovie = await MovieMediaItemsAsync(movies.Select(m => m.Id).ToList(), cancellationToken);
        var mediaByEpisode = await EpisodeMediaItemsAsync(episodes.Select(e => e.Id).ToList(), cancellationToken);

        // Strand keys drive the series interleaving (N19): every show is its own strand so its
        // episodes continue in air order at a stable slot position; movies share the "other"
        // strand and keep flowing through it in release order like a series of their own.
        var movieItems = movies
            .Select(m => TryMovieItem(m, mediaByMovie, access, summary))
            .Where(i => i is not null)
            .Cast<SchedulableItem>()
            .Select(i => i with { StrandKey = OtherStrandKey });
        var episodeItems = episodes
            .Select(e => (Item: ToItem(new ProxyEpisode(e.Id, e.MediaSourceId), e.ShowId, mediaByEpisode, access, summary), e.ShowId))
            .Where(x => x.Item is not null)
            .Select(x => x.Item! with { StrandKey = $"show:{x.ShowId}" });
        return movieItems.Concat(episodeItems).ToList();
    }

    // Deferred expansion pass for trailer-material rules: runs after the occurrences are
    // materialized so freshly scheduled movie airings of this run count as "movie in the plan".
    private async Task ExpandTrailerStatesAsync(
        Dictionary<long, RuleState> states, Dictionary<long, IReadOnlyList<ProgramRuleCursor>> cursors,
        AccessContext access, IReadOnlyDictionary<long, List<DateTime>> airingsByMovie,
        DateTime coverageStart, SummaryCounters summary, CancellationToken cancellationToken)
    {
        foreach (var state in states.Values.Where(s => s.Rule.MaterialType == ProgramRuleMaterialType.Trailer))
        {
            var items = await ExpandTrailersAsync(access, airingsByMovie, coverageStart, summary, cancellationToken);
            if (items.Count == 0)
            {
                summary.OrphanedRules++;
                _logger.LogInformation(
                    "[ProgramSchedule] Regel {RuleId} ({Name}) liefert kein planbares Material (verwaist, ohne Laufzeit oder nicht freigegeben).",
                    state.Rule.Id, state.Rule.Name);
            }
            state.SetItems(items, cursors.GetValueOrDefault(state.Rule.Id) ?? []);
        }
    }

    // Expands the "trailer of upcoming movies" material: every trailer link whose movie airs in
    // the covered window, deduplicated by media item and ordered by the movie's next airing so
    // the soonest trailer leads. The per-gap lead check in FillGaps applies the configured
    // TrailerLeadDays window on top of this candidate set.
    private async Task<List<SchedulableItem>> ExpandTrailersAsync(
        AccessContext access, IReadOnlyDictionary<long, List<DateTime>> airingsByMovie,
        DateTime coverageStart, SummaryCounters summary, CancellationToken cancellationToken)
    {
        var links = await _db.MovieMediaItems
            .AsNoTracking()
            .Where(l => l.IsTrailer)
            .Select(l => new
            {
                l.MovieId,
                l.MediaItemId,
                l.MediaItem.Duration,
                l.MediaItem.DurationProbedAt,
                l.MediaItem.DurationProbeAttempts,
                l.Movie.MediaSourceId,
                l.Movie.MovieCollectionId
            })
            .ToListAsync(cancellationToken);
        if (links.Count == 0)
            return [];

        var items = new List<SchedulableItem>();
        foreach (var link in links
                     .GroupBy(l => l.MediaItemId)
                     .Select(g => g.OrderBy(l => airingsByMovie.GetValueOrDefault(l.MovieId)?.FirstOrDefault() ?? DateTime.MaxValue).First())
                     .OrderBy(l => airingsByMovie.GetValueOrDefault(l.MovieId)?.FirstOrDefault() ?? DateTime.MaxValue))
        {
            summary.Candidates++;
            if (link.Duration is null)
            {
                CountNoDuration(new MediaItemRef(link.MediaItemId, link.Duration, link.DurationProbedAt, link.DurationProbeAttempts), summary);
                continue;
            }
            if (!access.Allows(link.MediaSourceId, null, link.MovieCollectionId))
            {
                summary.SkippedNoAccess++;
                continue;
            }
            if (!airingsByMovie.TryGetValue(link.MovieId, out var starts) || !starts.Exists(s => s > coverageStart))
                continue; // movie has no upcoming airing in the covered window
            items.Add(new SchedulableItem(ProgramCursorContentType.Trailer, link.MediaItemId, link.MediaItemId, link.MovieId, null, link.Duration.Value));
        }
        return items;
    }

    // Index of the first item in the filler ring whose movie airs within (fillAt, leadEnd] —
    // the "trailer leads the movie" check evaluated per gap position.
    private static int? FindLeadMatchOffset(RuleState state, DateTime fillAt, DateTime leadEnd, IReadOnlyDictionary<long, List<DateTime>> airingsByMovie)
    {
        for (var i = 0; i < state.Items.Count; i++)
        {
            var item = state.PeekAt(i);
            if (item?.MovieId is { } movieId
                && airingsByMovie.TryGetValue(movieId, out var starts)
                && starts.Exists(s => s > fillAt && s <= leadEnd))
                return i;
        }
        return null;
    }

    private async Task<HashSet<long>> TrailerMediaItemIdsAsync(CancellationToken cancellationToken)
        => (await _db.MovieMediaItems
                .Where(l => l.IsTrailer)
                .Select(l => l.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

    // Expands the "clip source" material: every unclassified video media item of the referenced
    // media source — files that were never turned into a movie or episode entry (no NFO), e.g.
    // mascot/interstitial clips in a dedicated source. Items linked to a movie or episode are
    // excluded so regularly classified content stays out of the filler ring even when the user
    // points the rule at a normal library source. Ordered by media item id for a stable ring.
    private async Task<List<SchedulableItem>> ExpandMediaSourceClipsAsync(
        long sourceId, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        if (!await _db.MediaSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            summary.MaterialMissing++;
            return [];
        }

        var items = await _db.MediaItems
            .AsNoTracking()
            .Where(mi => mi.MediaCollection.MediaSourceId == sourceId)
            .Select(mi => new { mi.Id, mi.Path, mi.Duration, mi.DurationProbedAt, mi.DurationProbeAttempts })
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
            return [];

        var linkedMovieItems = (await _db.MovieMediaItems
                .Where(l => items.Select(i => i.Id).Contains(l.MediaItemId))
                .Select(l => l.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var linkedEpisodeItems = (await _db.TVShowEpisodeMediaItems
                .Where(l => items.Select(i => i.Id).Contains(l.MediaItemId))
                .Select(l => l.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var result = new List<SchedulableItem>();
        foreach (var item in items.OrderBy(i => i.Id))
        {
            if (!Utils.VideoFileExtensions.IsVideoFileName(item.Path))
                continue;
            if (linkedMovieItems.Contains(item.Id) || linkedEpisodeItems.Contains(item.Id))
                continue; // classified content is regular material, not filler clips
            summary.Candidates++;
            if (item.Duration is null)
            {
                CountNoDuration(new MediaItemRef(item.Id, item.Duration, item.DurationProbedAt, item.DurationProbeAttempts), summary);
                continue;
            }
            if (!access.Allows(sourceId, null, null))
            {
                summary.SkippedNoAccess++;
                continue;
            }
            result.Add(new SchedulableItem(ProgramCursorContentType.MediaItem, item.Id, item.Id, null, null, item.Duration.Value));
        }
        return result;
    }

    private async Task<Dictionary<long, List<DateTime>>> LoadMovieAiringsAsync(long channelId, DateTime afterUtc, IEnumerable<ProgramSlot> extraSlots, CancellationToken cancellationToken)
    {
        var trailerItemIds = await TrailerMediaItemIdsAsync(cancellationToken);
        var rows = await _db.ProgramSlots
            .Where(s => s.TVChannelId == channelId && s.Status == ProgramSlotStatus.Scheduled && s.MovieId != null && s.Start > afterUtc)
            .Select(s => new { s.MovieId, s.MediaItemId, s.Start })
            .ToListAsync(cancellationToken);
        return BuildMovieAiringsMap(
            rows.Select(r => (r.MovieId, r.MediaItemId, r.Start))
                .Concat(extraSlots.Select(s => (s.MovieId, s.MediaItemId, s.Start))),
            afterUtc, trailerItemIds);
    }

    private async Task<Dictionary<long, List<DateTime>>> BuildMovieAiringsFromSlotsAsync(IEnumerable<ProgramSlot> slots, DateTime afterUtc, CancellationToken cancellationToken)
    {
        var trailerItemIds = await TrailerMediaItemIdsAsync(cancellationToken);
        return BuildMovieAiringsMap(
            slots.Select(s => (s.MovieId, s.MediaItemId, s.Start)),
            afterUtc, trailerItemIds);
    }

    // Groups movie airing starts per movie; slots produced by trailer material itself (media item
    // is a trailer link) never count as an airing of the movie.
    private static Dictionary<long, List<DateTime>> BuildMovieAiringsMap(
        IEnumerable<(long? MovieId, long MediaItemId, DateTime Start)> slots, DateTime afterUtc, HashSet<long> trailerItemIds)
    {
        var map = new Dictionary<long, List<DateTime>>();
        foreach (var s in slots)
        {
            if (s.MovieId is not { } movieId || s.Start <= afterUtc || trailerItemIds.Contains(s.MediaItemId))
                continue;
            (map.TryGetValue(movieId, out var list) ? list : map[movieId] = []).Add(s.Start);
        }
        foreach (var list in map.Values)
            list.Sort();
        return map;
    }

    private async Task<List<SchedulableItem>> ExpandPlaylistAsync(long playlistId, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);
        if (playlist is null)
        {
            summary.MaterialMissing++;
            return [];
        }

        var entries = await _db.PlaylistEntries
            .Where(e => e.PlaylistId == playlistId)
            .ToListAsync(cancellationToken);
        entries = entries
            .Where(e => PlaylistEntryMediaTypeResolver.IsPlayable(e.MediaType))
            .ToList();
        if (entries.Count == 0)
            return [];

        if (playlist.SortMode == PlaylistSortMode.Manual)
        {
            entries = entries.OrderBy(e => e.SortOrder).ThenBy(e => e.AddedAt).ToList();
        }
        else
        {
            var releaseDates = new Dictionary<(string Type, long Id), DateTime?>();
            var byType = MediaHierarchyRegistry.GroupMediaIdsByType(entries);
            foreach (var (mediaType, ids) in byType)
            {
                if (!MediaHierarchyRegistry.TryParseKnownMediaType(mediaType, out var parsed))
                    continue;
                var dates = await MediaHierarchyRegistry.Handlers[parsed].LoadReleaseDateAsync(_db, ids, cancellationToken);
                foreach (var (id, date) in dates)
                    releaseDates[(mediaType, id)] = date;
            }
            entries = entries
                .OrderBy(e => releaseDates.GetValueOrDefault((e.MediaType, e.MediaId)))
                .ThenBy(e => e.AddedAt)
                .ThenBy(e => e.Id)
                .ToList();
        }

        var movieIds = entries.Where(e => e.MediaType == MediaTypeValues.Movie).Select(e => e.MediaId).ToList();
        var episodeIds = entries.Where(e => e.MediaType == MediaTypeValues.TVShowEpisode).Select(e => e.MediaId).ToList();

        var movies = await _db.Movies.Where(m => movieIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, cancellationToken);
        var episodeShowIds = await _db.TVShowEpisodes
            .Where(e => episodeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.MediaSourceId, ShowId = e.TVShowSeason.TVShowId })
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var mediaByMovie = await MovieMediaItemsAsync(movieIds, cancellationToken);
        var mediaByEpisode = await EpisodeMediaItemsAsync(episodeIds, cancellationToken);

        var items = new List<SchedulableItem>();
        foreach (var entry in entries)
        {
            if (entry.MediaType == MediaTypeValues.Movie && movies.TryGetValue(entry.MediaId, out var movie))
            {
                var item = TryMovieItem(movie, mediaByMovie, access, summary);
                if (item is not null)
                    items.Add(item with { ContentType = ProgramCursorContentType.PlaylistEntry, ContentId = entry.Id });
            }
            else if (entry.MediaType == MediaTypeValues.TVShowEpisode && episodeShowIds.TryGetValue(entry.MediaId, out var episode))
            {
                var item = ToItem(new ProxyEpisode(episode.Id, episode.MediaSourceId), episode.ShowId, mediaByEpisode, access, summary);
                if (item is not null)
                    items.Add(item with { ContentType = ProgramCursorContentType.PlaylistEntry, ContentId = entry.Id });
            }
            // Orphaned entries (referenced media no longer exists) are skipped silently.
        }
        return items;
    }
}
