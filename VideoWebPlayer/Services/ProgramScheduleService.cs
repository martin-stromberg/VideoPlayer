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

    // Horizon of the diagnosis occurrence search: wide enough to find the next start of every
    // daily/weekly recurrence (a week plus margin). A rule without an occurrence inside it —
    // e.g. a SingleDate further out — is evaluated at its own TimeOfDay on the current local
    // day: only the local time-of-day feeds the window check, so that yields the same instant
    // the real next occurrence would produce.
    private const int DiagnosisOccurrenceHorizonDays = 9;

    private readonly ApplicationDbContext _db;
    private readonly IProgramRuleService _ruleService;
    private readonly ProgramSettingsService _settings;
    private readonly ILogger<ProgramScheduleService> _logger;
    private readonly int _maxProbeAttempts;
    private readonly TimeSpan _trailerLead;
    private readonly TimeSpan _minStartRemain;
    private readonly TimeSpan _maxOccurrenceLookback;

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
        _maxOccurrenceLookback = schedule.EffectiveMaxOccurrenceLookback;
    }

    /// <inheritdoc />
    public async Task<ProgramScheduleRunSummary> EnsureCoverageAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var channel = await _ruleService.GetOrCreateChannelAsync(cancellationToken);
        var rules = await _db.ProgramRules
            .Include(r => r.Genres)
            .Include(r => r.MediaSourceFilters)
            .Include(r => r.ClipGroups)
                .ThenInclude(c => c.ClipGroup)
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
        var ageRule = await LoadAgeRuleAsync(cancellationToken);
        var ruleIds = rules.Select(r => r.Id).ToList();
        var cursors = (await _db.ProgramRuleCursors
                .Where(c => ruleIds.Contains(c.ProgramRuleId))
                .ToListAsync(cancellationToken))
            .GroupBy(c => c.ProgramRuleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProgramRuleCursor>)g.ToList());

        var states = new Dictionary<long, RuleState>();
        var clipContext = new ClipFillContext(access, null, coverageStart, [], horizonEnd.UtcDateTime, summary, ageRule);
        foreach (var rule in rules)
        {
            // Trailer material expands only after the occurrences are materialized — its content
            // set depends on the freshly computed movie airings of this very run.
            var items = rule.MaterialType == ProgramRuleMaterialType.Trailer
                ? new List<SchedulableItem>()
                : await ExpandMaterialAsync(rule, access, summary, null, coverageStart, cancellationToken);
            var state = new RuleState(rule, items, cursors.GetValueOrDefault(rule.Id) ?? []);
            // Trailer clip groups expand late (after materialization) — their candidate set
            // depends on the freshly computed movie airings of this run.
            state.ClipGroups.AddRange(await BuildClipGroupStates(
                rule, cursors.GetValueOrDefault(rule.Id) ?? [], clipContext, cancellationToken));
            states[rule.Id] = state;
        }

        var blocked = (await _db.ProgramSlots
                .Where(s => s.TVChannelId == channel.Id && s.Status == ProgramSlotStatus.Scheduled && s.End > coverageStart && s.Start < horizonEnd.UtcDateTime)
                .Select(s => new { s.Start, s.End })
                .ToListAsync(cancellationToken))
            .Select(s => new Interval(s.Start, s.End))
            .ToList();

        var newSlots = new List<ProgramSlot>();
        var occurrences = BuildOccurrences(rules.Where(r => r.RuleType != ProgramRuleType.Filler), coverageStart, horizonEnd, federalState, OccurrenceLookback);
        var boundStarts = BoundStarts(occurrences, states);
        var occurrenceStarts = occurrences
            .GroupBy(o => o.Rule.Id)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DateTime>)g.Select(o => o.StartUtc).OrderBy(s => s).ToList());
        foreach (var occurrence in occurrences.OrderByDescending(o => o.Rule.Priority).ThenBy(o => o.Rule.Id).ThenBy(o => o.StartUtc))
            MaterializeOccurrence(occurrence, states, blocked, newSlots, coverageStart, BoundaryAfter(boundStarts, occurrence.StartUtc, occurrence.Rule.Priority), summary, ageRule);

        var movieAirings = await LoadMovieAiringsAsync(channel.Id, coverageStart, newSlots, cancellationToken);
        await ExpandTrailerStatesAsync(states, cursors, access, movieAirings, coverageStart, summary, cancellationToken);
        clipContext = clipContext with { AiringsByMovie = movieAirings, BoundStarts = boundStarts, OccurrenceStarts = occurrenceStarts };
        await ExpandClipGroupStatesAsync(states, cursors, clipContext, cancellationToken);
        InsertClipFill(newSlots, blocked, states, clipContext);
        FillGaps(states, rules, blocked, newSlots, coverageStart, horizonEnd.UtcDateTime, summary, ageRule, movieAirings);

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
            .Include(r => r.ClipGroups)
                .ThenInclude(c => c.ClipGroup)
            .Where(r => r.TVChannelId == channel.Id)
            .ToListAsync(cancellationToken);
        var rulesById = rules.ToDictionary(r => r.Id);
        var activeRules = rules.Where(r => r.IsActive).ToList();

        var horizonEnd = utcNow + await _settings.GetProgramScheduleHorizonAsync(cancellationToken);
        var coverageStart = utcNow.UtcDateTime;

        var summary = new SummaryCounters();
        var access = await LoadAccessContextAsync(cancellationToken);
        var federalState = await _settings.GetHolidayFederalStateAsync(cancellationToken);
        var ageRule = await LoadAgeRuleAsync(cancellationToken);

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
        var clipContext = new ClipFillContext(access, airingsByMovie, coverageStart, [], horizonEnd.UtcDateTime, summary, ageRule);
        foreach (var rule in rules)
        {
            var items = await ExpandMaterialAsync(rule, access, summary, airingsByMovie, coverageStart, cancellationToken);
            var state = new RuleState(rule, items, cursors.GetValueOrDefault(rule.Id) ?? []);
            // Reconcile knows the persisted movie airings already — trailer clip groups expand
            // immediately, like the trailer material.
            state.ClipGroups.AddRange(await BuildClipGroupStates(
                rule, cursors.GetValueOrDefault(rule.Id) ?? [], clipContext, cancellationToken));
            states[rule.Id] = state;
        }

        // Scheduled slots reaching into the lookback window — wide enough to capture the head slot
        // of airings that are still running. No upper bound on Start: an airing overshooting the
        // horizon must be loaded completely, otherwise the truncated tail fails the sequence check
        // and gets displaced — which then re-materializes the same positions and piles up
        // overlapping displaced duplicates run over run. The window is widened on demand up to
        // the configured maximum lookback: airings whose real start lies more than a day back
        // (marathon blocks, multi-day broadcasts) need their head slots and invader evidence
        // loaded for the reproducibility check.
        // Displaced rows ride along in the same query — invader evidence only: their occupancy
        // at the materialization time is what legitimately shifted an airing, and it stays
        // provable after the invader itself is displaced (Reload keeps them out of
        // Airings/Scheduled).
        async Task<(List<ProgramSlot> Scheduled, List<ProgramSlot> DisplacedEvidence)> LoadWindowAsync(DateTime from)
        {
            // The split keys off the projected database status, not the entity property: a
            // tracked instance keeps a stale Status after a bulk ExecuteUpdate (e.g. the admin
            // cleanup of displaced rows) and would otherwise be sorted into the wrong list.
            var rows = await _db.ProgramSlots
                .Where(s => s.TVChannelId == channel.Id && s.End > from
                    && (s.Status == ProgramSlotStatus.Scheduled || s.Status == ProgramSlotStatus.Displaced))
                .OrderBy(s => s.Start)
                .Select(s => new { Slot = s, s.Status })
                .ToListAsync(cancellationToken);
            return (rows.Where(r => r.Status == ProgramSlotStatus.Scheduled).Select(r => r.Slot).ToList(),
                rows.Where(r => r.Status == ProgramSlotStatus.Displaced).Select(r => r.Slot).ToList());
        }

        // Everything derived from the loaded slot window lives in the ReconcileWindow — Reload()
        // re-derives and re-validates it atomically on every (re-)load, so no structure of a
        // narrower window can be reused by mistake.
        var window = new ReconcileWindow(coverageStart, horizonEnd, federalState, activeRules, states, rulesById, _minStartRemain, ageRule, _logger);

        // Cost heuristic only — carries no correctness weight: a slot starting at or before the
        // base window edge proves that pre-window context exists, so the window is widened once
        // and the validation runs a single time on the full window. If the trigger misses (a
        // partially materialized head leaves no slot row at the edge, or a Scheduled gap covers
        // it), the validation-driven widening below decides instead.
        var loadFrom = coverageStart - OccurrenceLookback;
        var (scheduled, displacedEvidence) = await LoadWindowAsync(loadFrom);
        var widened = scheduled.Any(s => s.Start <= loadFrom);
        if (widened)
        {
            loadFrom = coverageStart - _maxOccurrenceLookback;
            (scheduled, displacedEvidence) = await LoadWindowAsync(loadFrom);
        }
        window.Reload(loadFrom, scheduled, displacedEvidence);

        // Validation-driven widening (only when the trigger did not already widen): an invalid
        // airing of an active rule reaching into the coverage may merely miss pre-window context
        // — widen once, reload and re-validate on the full window. Genuinely stale airings stay
        // invalid either way (the second pass costs a bounded reload, nothing more).
        if (!widened && window.HasRescuableInvalid)
        {
            var widenedFrom = coverageStart - _maxOccurrenceLookback;
            var (widenedScheduled, widenedDisplaced) = await LoadWindowAsync(widenedFrom);
            window.Reload(widenedFrom, widenedScheduled, widenedDisplaced);
        }

        var displaced = new List<ProgramSlot>();
        var displacedRuleIds = new HashSet<long>();

        void Displace(ProgramSlot slot)
        {
            if (slot.End <= coverageStart)
                return; // fully aired — stays part of history
            slot.Status = ProgramSlotStatus.Displaced;
            displaced.Add(slot);
        }

        // Suffix displacement is applied only now, on the final validation result: on the first
        // invalid airing of a rule the whole remaining future of that rule is displaced
        // (sequence context is cursor-based — later airings depend on it).
        foreach (var (ordered, firstInvalid) in window.InvalidGroups)
        {
            foreach (var airing in ordered.Skip(firstInvalid))
            {
                foreach (var slot in airing)
                    Displace(slot);
                if (airing[0].ProgramRuleId is { } displacedRuleId)
                    displacedRuleIds.Add(displacedRuleId);
            }
        }

        foreach (var slot in window.FillerSlots.Where(s => s.End > coverageStart))
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
        var keptSlots = window.Scheduled.Where(s => s.Status == ProgramSlotStatus.Scheduled && s.End > coverageStart).ToList();
        var newSlots = new List<ProgramSlot>();
        var blocked = keptSlots.Select(s => new Interval(s.Start, s.End)).ToList();

        void DisplaceRuleFuture(long ruleId, DateTime fromStartUtc)
        {
            foreach (var airing in window.Airings.Where(a => a[0].ProgramRuleId == ruleId && a[0].Start >= fromStartUtc))
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

            foreach (var slot in window.FillerSlots.Where(s => s.ProgramRuleId == ruleId && s.Start >= fromStartUtc && s.Status == ProgramSlotStatus.Scheduled))
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

        // Occurrences from the widened lookback serve only as reproduction anchors — re-materializing
        // them could write shifted content behind the kept tail that the next run could not prove
        // again (oscillation). Re-materialization therefore stays on the base window.
        foreach (var occurrence in window.Occurrences.Where(o => o.StartUtc >= coverageStart - OccurrenceLookback).OrderByDescending(o => o.Rule.Priority).ThenBy(o => o.Rule.Id).ThenBy(o => o.StartUtc))
        {
            var losers = MaterializeOccurrenceEvicting(occurrence, states, keptSlots, blocked, newSlots, coverageStart, BoundaryAfter(window.BoundStarts, occurrence.StartUtc, occurrence.Rule.Priority), summary, Evicts, ageRule);
            if (losers is not null && losers.Count > 0)
            {
                foreach (var loser in losers)
                {
                    if (loser.ProgramRuleId is not { } loserRuleId)
                        continue;
                    var airing = window.Airings.FirstOrDefault(a => a.Contains(loser));
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
        InsertClipFill(newSlots, blocked, states,
            clipContext with
            {
                AiringsByMovie = leadAirings,
                BoundStarts = window.BoundStarts,
                OccurrenceStarts = window.Occurrences
                    .GroupBy(o => o.Rule.Id)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<DateTime>)g.Select(o => o.StartUtc).OrderBy(s => s).ToList())
            });
        FillGaps(states, activeRules, blocked, newSlots, coverageStart, horizonEnd.UtcDateTime, summary, ageRule, leadAirings);

        PersistCursors(states, cursors);
        _db.ProgramSlots.AddRange(newSlots);
        await _db.SaveChangesAsync(cancellationToken);

        var result = new ProgramScheduleReconcileSummary(
            displaced.Count, summary.SlotsCreated, summary.FillerSlotsCreated,
            summary.SkippedNoDuration, summary.SkippedNoAccess, summary.OrphanedRules,
            summary.DroppedConflicts, summary.RemainingGapSeconds, summary.BlockedByAgeWindow);
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
        var federalState = await _settings.GetHolidayFederalStateAsync(cancellationToken);
        var ageRule = await LoadAgeRuleAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var airingsByMovie = await LoadMovieAiringsAsync(channelId, nowUtc, [], cancellationToken);

        // Evaluation time of a rule's window check: its next upcoming occurrence start —
        // the same local time-of-day the materialization would plan against. Without an
        // occurrence inside the horizon the rule's own TimeOfDay on the current day is the
        // equivalent evaluation instant (see DiagnosisOccurrenceHorizonDays); filler rules
        // have no nominal start at all and evaluate at "now".
        var nextStarts = BuildOccurrences(rules.Where(r => r.RuleType != ProgramRuleType.Filler),
                nowUtc, new DateTimeOffset(nowUtc.AddDays(DiagnosisOccurrenceHorizonDays)), federalState, TimeSpan.Zero)
            .GroupBy(o => o.Rule.Id)
            .ToDictionary(g => g.Key, g => g.Min(o => o.StartUtc));

        var diagnoses = new Dictionary<long, ProgramRuleDiagnosis>();
        foreach (var rule in rules)
        {
            // Fresh counters per rule — the same expansion the schedule run uses, with the skip
            // reasons tallied into a per-rule diagnosis instead of the run summary.
            var counters = new SummaryCounters();
            var items = await ExpandMaterialAsync(rule, access, counters, airingsByMovie, nowUtc, cancellationToken);
            var evaluationStart = nextStarts.TryGetValue(rule.Id, out var nextStart)
                ? nextStart
                : rule.RuleType == ProgramRuleType.Filler
                    ? nowUtc
                    : TimeZoneInfo.ConvertTimeToUtc(
                        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, TimeZoneInfo.Local))
                            .ToDateTime(rule.TimeOfDay),
                        TimeZoneInfo.Local);
            diagnoses[rule.Id] = new ProgramRuleDiagnosis(
                rule.Id,
                counters.MaterialMissing > 0,
                counters.Candidates,
                items.Count,
                counters.NoMediaItem,
                counters.ProbePending,
                counters.ProbeExhausted,
                counters.SkippedNoAccess,
                ProgramScheduleAgeWindow.CountBlockedAt(
                    items.Select(i => i.AgeRatingMinAge), evaluationStart, ageRule));
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
    public async Task<int> CleanupSelfDisplacedSlotsAsync(CancellationToken cancellationToken = default)
    {
        var channel = await _ruleService.GetOrCreateChannelAsync(cancellationToken);
        // Strict twin criterion: a displaced row is only a proven self-displacement duplicate
        // when a scheduled row of the same producing rule occupies the identical interval with
        // the same content. The scheduled twin covers the interval completely — the occupancy
        // evidence of the following reconcile run is unchanged, so no evidence is lost.
        var deleted = await _db.ProgramSlots
            .Where(d => d.TVChannelId == channel.Id && d.Status == ProgramSlotStatus.Displaced)
            .Where(d => _db.ProgramSlots.Any(s => s.TVChannelId == d.TVChannelId
                && s.Status == ProgramSlotStatus.Scheduled
                && s.Start == d.Start && s.End == d.End
                && s.ProgramRuleId == d.ProgramRuleId
                && s.IsClipFill == d.IsClipFill
                && (s.MediaItemId == d.MediaItemId
                    || (d.MovieId != null && s.MovieId == d.MovieId)
                    || (d.TVShowEpisodeId != null && s.TVShowEpisodeId == d.TVShowEpisodeId))))
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation("[ProgramSchedule] {Deleted} Selbst-Verdraengungs-Duplikate bereinigt.", deleted);
        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredRulesAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var channel = await _ruleService.GetOrCreateChannelAsync(cancellationToken);
        var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow.UtcDateTime, TimeZoneInfo.Local));

        // Nur einmalige Zeiträume laufen ab — einseitig offene Regeln ohne Bis-Datum und alle
        // wiederkehrenden Fenster bleiben bestehen. Die FK-Kaskade entfernt Fortsetzungsstände
        // und Join-Zeilen mit; die Sendeplätze bleiben als weiche Referenz (Historie) erhalten
        // und werden vom folgenden Reconcile-Lauf verdrängt. Das Ablauf-Prädikat selbst liegt
        // in ProgramRuleValidity.IsExpired — die EF-Abfrage grenzt nur grob ein.
        var candidates = await _db.ProgramRules
            .Where(r => r.TVChannelId == channel.Id
                && r.ValidityMode == ProgramRuleValidityMode.OneTime
                && r.ValidTo != null)
            .ToListAsync(cancellationToken);
        var expired = candidates.Where(r => ProgramRuleValidity.IsExpired(r, localDay)).ToList();
        if (expired.Count == 0)
            return 0;

        _db.ProgramRules.RemoveRange(expired);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[ProgramSchedule] {Deleted} abgelaufene Programmregeln bereinigt.", expired.Count);
        return expired.Count;
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

    // Recursion-invariant context of the airing-reproducibility check plus the memo shared
    // across the recursion — bundled so the dispatch to the flat/interleaved tail checks keeps
    // a short (airing, offset, afterStart) signature.
    private sealed record ReproContext(
        RuleState State,
        HashSet<DateTime> OccurrenceStarts,
        List<(DateTime StartUtc, int Priority)> BoundStarts,
        IReadOnlyList<Interval> OccupiedEvidence,
        TimeSpan MinStartRemain,
        ProgramScheduleAgeWindowRule AgeRule,
        Dictionary<(int Offset, DateTime AfterStart), bool> Memo)
    {
        // Diagnosis sink: the deepest check stage any hypothesis reached before failing wins
        // (the enum is stage-ordered), so the reported reason is the most informative one —
        // not the first failure of an early hypothesis.
        public AiringInvalidReason InvalidReason { get; private set; } = AiringInvalidReason.None;

        public void NoteFailure(AiringInvalidReason reason)
        {
            if (reason > InvalidReason)
                InvalidReason = reason;
        }
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
    // Media item ids of every clip-group ring of the rule — the clip-first classification set:
    // a slot whose media item is a clip candidate is a fill clip, everything else a title.
    private static HashSet<long> ClipMediaItemIds(RuleState state)
    {
        var ids = new HashSet<long>();
        foreach (var clipGroup in state.ClipGroups)
            foreach (var item in clipGroup.Ring.Items)
                ids.Add(item.MediaItemId);
        return ids;
    }

    private static bool AiringReproducible(List<ProgramSlot> airing, RuleState state, HashSet<DateTime> occurrenceStarts, List<(DateTime StartUtc, int Priority)> boundStarts, IReadOnlyList<Interval> occupiedEvidence, TimeSpan minStartRemain, ProgramScheduleAgeWindowRule ageRule, out AiringInvalidReason invalidReason)
    {
        var clipIds = ClipMediaItemIds(state);
        var effective = airing;
        if (clipIds.Count > 0)
        {
            // Clip-aware pre-filter: clip slots are verified by membership in the current clip
            // expansion (a removed/changed clip makes the airing invalid — same behavior as
            // changed material) and by gapless chaining; their positions are not re-simulated.
            // For the title tail check the kept slots are projected onto a clip-free time axis:
            // every title slot shifts back by the clip time preceding it, so content, order and
            // durations reproduce under the regular check.
            effective = new List<ProgramSlot>(airing.Count);
            var clipTime = TimeSpan.Zero;
            for (var i = 0; i < airing.Count; i++)
            {
                var slot = airing[i];
                if (i > 0 && slot.Start != airing[i - 1].End)
                {
                    invalidReason = AiringInvalidReason.ClipChainBroken; // a clip airing must stay gaplessly linked
                    return false;
                }
                if (clipIds.Contains(slot.MediaItemId))
                {
                    clipTime += slot.End - slot.Start;
                    continue;
                }
                effective.Add(new ProgramSlot
                {
                    Start = slot.Start - clipTime,
                    End = slot.End - clipTime,
                    MediaItemId = slot.MediaItemId,
                    MovieId = slot.MovieId,
                    TVShowEpisodeId = slot.TVShowEpisodeId,
                    ProgramRuleId = slot.ProgramRuleId,
                    RuleType = slot.RuleType,
                    Status = slot.Status
                });
            }
            if (effective.Count == 0)
            {
                invalidReason = AiringInvalidReason.TitlelessAiring; // an airing always carries at least one title
                return false;
            }
        }
        var context = new ReproContext(state, occurrenceStarts, boundStarts, occupiedEvidence, minStartRemain, ageRule, new Dictionary<(int Offset, DateTime AfterStart), bool>());
        if (AiringReproducible(effective, 0, DateTime.MinValue, context))
        {
            invalidReason = AiringInvalidReason.None;
            return true;
        }
        invalidReason = context.InvalidReason;
        return false;
    }

    // Validates airing[offset..] as a chain of sub-airings anchored at strictly ascending
    // occurrence starts (> afterStart): a merged group of a dense rule holds one (partial)
    // airing per occurrence, so the check decomposes it recursively. Each sub-airing keeps the
    // full original proof — anchor, invader evidence and slot-by-slot match — and consumes at
    // least one slot, so the recursion depth is bounded by the group length; the memo keyed by
    // (rest head, lower occurrence bound) bounds the branching cost of pathological groups.
    private static bool AiringReproducible(List<ProgramSlot> airing, int offset, DateTime afterStart, ReproContext context)
    {
        if (offset >= airing.Count)
            return true;
        if (context.Memo.TryGetValue((offset, afterStart), out var cached))
            return cached;

        var result = context.State.IsInterleaved
            ? InterleavedAiringTailReproducible(airing, offset, afterStart, context)
            : FlatAiringTailReproducible(airing, offset, afterStart, context);
        context.Memo[(offset, afterStart)] = result;
        return result;
    }

    private static bool FlatAiringTailReproducible(List<ProgramSlot> airing, int offset, DateTime afterStart, ReproContext context)
    {
        var (state, occurrenceStarts, boundStarts, occupiedEvidence, minStartRemain, ageRule, _) = context;
        var items = state.Items;
        if (items.Count == 0)
        {
            context.NoteFailure(AiringInvalidReason.HeadItemNotInExpansion);
            return false;
        }
        if (occurrenceStarts.Count == 0)
        {
            context.NoteFailure(AiringInvalidReason.NoOccurrenceAnchor);
            return false;
        }

        var idx = FindItemIndex(items, airing[offset]);
        if (idx < 0)
        {
            context.NoteFailure(AiringInvalidReason.HeadItemNotInExpansion);
            return false;
        }

        var rule = state.Rule;
        var wanted = rule.EpisodeCount is > 0 ? rule.EpisodeCount.Value : int.MaxValue;
        var clipGrace = state.ClipGrace;
        var preservesOrder = state.PreservesOrder;

        var count = items.Count;
        var earliestStart = occurrenceStarts.Min();
        // Backward hypotheses: k = taken-but-unwritten head items. The scan head may
        // additionally lie behind window-skipped positions — a skipped position consumed the
        // ring without producing airtime, so it counts toward the head index but not toward
        // the reconstructed start. A predecessor position was skipped exactly when it was
        // blocked at the next taken item's start (skips advance no airtime, so the selection
        // evaluated them at that pick's slot time).
        for (var k = 0; k < wanted; k++)
        {
            var skipped = TimeSpan.Zero;
            var positionsBack = 0;
            var takenBack = 0;
            var skipRun = 0;
            var possible = true;
            while (takenBack < k)
            {
                var p = ((idx - positionsBack - 1) % count + count) % count;
                var item = items[p];
                if (!preservesOrder
                    && !ProgramScheduleAgeWindow.IsAllowedAt(
                        item.AgeRatingMinAge, airing[offset].Start - skipped, ageRule))
                {
                    positionsBack++;
                    if (++skipRun >= count)
                    {
                        possible = false; // a full ring lap of skips is impossible — the scan would have stopped
                        break;
                    }
                    continue;
                }
                skipRun = 0;
                if (item.Duration <= TimeSpan.Zero)
                {
                    possible = false; // degenerate duration — the backward walk would never progress
                    break;
                }
                skipped += item.Duration;
                positionsBack++;
                takenBack++;
            }
            // The backward hypotheses are deterministic: a walk that hit a wall (a full ring
            // lap of skips or a degenerate duration) fails identically for every larger k —
            // break, like the interleaved counterpart does, instead of scanning on forever
            // (wanted is int.MaxValue for rules without EpisodeCount).
            if (!possible)
                break;

            // The sub-airing's real start — its first taken item began here; the nominal
            // occurrence started at or before this point.
            var airingStart = airing[offset].Start - skipped;
            if (airingStart < earliestStart)
                break; // skipped duration already reaches past the oldest occurrence in the window

            var headIndex = ((idx - positionsBack) % count + count) % count;
            foreach (var occurrenceStart in occurrenceStarts.Where(o => o > afterStart && o <= airingStart))
            {
                // A shift needs invader evidence: a chain of occupied intervals covering the
                // nominal start whose end is exactly the real start — otherwise the earlier
                // anchor would rescue stale airings (e.g. after the rule's time-of-day changed).
                if (occurrenceStart < airingStart
                    && !ShiftProvable(occurrenceStart, airingStart, occupiedEvidence))
                {
                    context.NoteFailure(AiringInvalidReason.AnchorWithoutInvaderEvidence);
                    continue;
                }
                var expected = SelectAiringItems(items, headIndex, rule, airingStart,
                    BoundaryAfter(boundStarts, occurrenceStart, rule.Priority), minStartRemain, clipGrace,
                    preservesOrder, ageRule);
                // Hypothesis k claims exactly k taken-but-unwritten picks before the airing
                // head — the head is therefore pick #k of the simulation and must sit on the
                // observed ring position.
                var headPick = k < expected.Taken.Count && expected.Positions[k] == idx ? k : -1;
                if (headPick < 0)
                {
                    context.NoteFailure(AiringInvalidReason.HeadPickInconsistent);
                    continue;
                }
                // The candidate may cover a real prefix of the group — the merged chain holds
                // one airing per occurrence; the remainder is checked recursively against
                // strictly later occurrences.
                var consumed = expected.Taken.Count - headPick;
                if (consumed < 1 || consumed > airing.Count - offset)
                {
                    context.NoteFailure(AiringInvalidReason.SlotSequenceMismatch);
                    continue;
                }

                var start = airing[offset].Start;
                var matches = true;
                for (var i = 0; i < consumed; i++)
                {
                    var slot = airing[offset + i];
                    var item = expected.Taken[headPick + i];
                    if (!SlotMatchesItem(slot, item, start))
                    {
                        matches = false;
                        break;
                    }
                    start = slot.End;
                }
                if (!matches)
                {
                    context.NoteFailure(AiringInvalidReason.SlotSequenceMismatch);
                    continue;
                }
                if (AiringReproducible(airing, offset + consumed, occurrenceStart, context))
                    return true;
                context.NoteFailure(AiringInvalidReason.RemainderNotReproducible);
            }
        }
        return false;
    }

    // Shift proof for the reproducibility check: the airing's real start must be reachable
    // from the nominal occurrence start through a chain of occupied intervals — each link
    // strictly covering the running point, the same ShiftPastInvaders fixpoint semantics the
    // materialization applies, so single-invader and chained shifts are provable alike. Only
    // links ending at or before the real start take part: the evidence set is grown history
    // (Scheduled ∪ displaced rows) and may contain intervals reaching past it — they must not
    // push the proven end beyond the observed start.
    private static bool ShiftProvable(DateTime occurrenceStart, DateTime airingStart, IReadOnlyList<Interval> occupiedEvidence)
        => ShiftPastInvaders(occurrenceStart, occupiedEvidence.Where(i => i.End <= airingStart)) == airingStart;

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
    private static bool InterleavedAiringTailReproducible(List<ProgramSlot> airing, int offset, DateTime afterStart, ReproContext context)
    {
        var (state, occurrenceStarts, boundStarts, occupiedEvidence, minStartRemain, ageRule, _) = context;
        var strands = state.Strands;
        var m = strands.Count;
        var rule = state.Rule;
        var clipGrace = state.ClipGrace;
        if (occurrenceStarts.Count == 0)
        {
            context.NoteFailure(AiringInvalidReason.NoOccurrenceAnchor);
            return false;
        }

        // Locate each slot of the remaining tail inside its strand. Ordered strands keep
        // strict ring contiguity (episodes of a series never air shuffled); unordered rings
        // may contain window-skipped positions — every intermediate position must have been
        // blocked at the following pick's slot start (skips advance no airtime, so the
        // selection evaluated them at exactly that time).
        var rest = airing.Count - offset;
        var strandOf = new int[rest];
        var firstIndex = new int[m];
        var lastIndex = new int[m];
        var firstStart = new DateTime[m];
        var countInAiring = new int[m];
        Array.Fill(firstIndex, -1);
        for (var i = 0; i < rest; i++)
        {
            var s = -1;
            var idx = -1;
            for (var t = 0; t < m; t++)
            {
                idx = FindItemIndex(strands[t].Items, airing[offset + i]);
                if (idx >= 0)
                {
                    s = t;
                    break;
                }
            }
            if (s < 0)
            {
                context.NoteFailure(AiringInvalidReason.HeadItemNotInExpansion);
                return false;
            }

            strandOf[i] = s;
            if (firstIndex[s] < 0)
            {
                firstIndex[s] = idx;
                firstStart[s] = airing[offset + i].Start;
            }
            else if (strands[s].PreservesOrder)
            {
                if (idx != (lastIndex[s] + 1) % strands[s].Items.Count)
                {
                    context.NoteFailure(AiringInvalidReason.StrandSequenceBroken);
                    return false;
                }
            }
            else
            {
                var n = strands[s].Items.Count;
                var distance = ((idx - lastIndex[s]) % n + n) % n;
                if (distance == 0)
                    distance = n; // a full lap back to the same position
                for (var d = 1; d < distance; d++)
                {
                    if (ProgramScheduleAgeWindow.IsAllowedAt(
                            strands[s].Items[(lastIndex[s] + d) % n].AgeRatingMinAge,
                            airing[offset + i].Start, ageRule))
                    {
                        context.NoteFailure(AiringInvalidReason.StrandSequenceBroken);
                        return false; // an allowed intermediate position would have been taken
                    }
                }
            }
            lastIndex[s] = idx;
            countInAiring[s]++;
        }

        var wanted = rule.EpisodeCount is > 0 ? rule.EpisodeCount.Value : int.MaxValue;
        var maxPrefix = Math.Min(wanted, strands.Sum(s => s.Items.Count) + rest);

        foreach (var occurrenceStart in occurrenceStarts.Where(o => o > afterStart && o <= airing[offset].Start).OrderBy(o => o))
        {
            // The real start may shift across a UTC day boundary when invaders push it —
            // the materialization derives the start-strand offset from that shifted day,
            // so both plausible days are tried and checked for consistency below.
            var realDay = airing[offset].Start.Date;
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
                    {
                        context.NoteFailure(AiringInvalidReason.HeadPickInconsistent);
                        break;
                    }

                    // Reconstruct each strand's scan head: the scan consumes taken prefix picks
                    // plus window-skipped positions. Walking the ring back from the first
                    // airing index, a predecessor counts as skipped iff it was blocked at the
                    // strand's next taken pick's start — taken picks drive the reconstructed
                    // real start back, skipped positions only the scan head.
                    var cursorBack = new int[m];
                    var nextTakenStart = new DateTime[m];
                    for (var s = 0; s < m; s++)
                        nextTakenStart[s] = firstStart[s];
                    var simStart = airing[offset].Start;
                    var reconstructable = true;
                    for (var q = p - 1; q >= 0 && reconstructable; q--)
                    {
                        var s = (startOffset + q) % m;
                        var items = strands[s].Items;
                        var count = items.Count;
                        var skipRun = 0;
                        while (true)
                        {
                            var ringPos = ((firstIndex[s] - cursorBack[s] - 1) % count + count) % count;
                            var item = items[ringPos];
                            if (!strands[s].PreservesOrder
                                && !ProgramScheduleAgeWindow.IsAllowedAt(item.AgeRatingMinAge, nextTakenStart[s], ageRule))
                            {
                                cursorBack[s]++;
                                if (++skipRun >= count)
                                {
                                    reconstructable = false; // a full ring lap of skips is impossible
                                    break;
                                }
                                continue;
                            }
                            cursorBack[s]++;
                            simStart -= item.Duration;
                            nextTakenStart[s] = simStart;
                            break;
                        }
                    }
                    if (!reconstructable)
                    {
                        context.NoteFailure(AiringInvalidReason.HeadPickInconsistent);
                        break;
                    }

                    var simStrands = new List<StrandState>(m);
                    for (var s = 0; s < m; s++)
                    {
                        var items = strands[s].Items;
                        var head = firstIndex[s] >= 0
                            ? ((firstIndex[s] - cursorBack[s]) % items.Count + items.Count) % items.Count
                            : 0;
                        simStrands.Add(new StrandState(strands[s].Key, items, head));
                    }

                    // The materialization derived the start-strand offset from the real start's
                    // day — a hypothesis is only consistent when it yields the same offset.
                    if (StrandStartOffset(wanted, m, simStart) != startOffset)
                    {
                        context.NoteFailure(AiringInvalidReason.HeadPickInconsistent);
                        continue;
                    }

                    if (occurrenceStart > simStart
                        || (occurrenceStart < simStart
                            && !ShiftProvable(occurrenceStart, simStart, occupiedEvidence)))
                    {
                        context.NoteFailure(AiringInvalidReason.AnchorWithoutInvaderEvidence);
                        continue; // shift requires an occupied chain ending exactly at the real start
                    }

                    var picks = SelectAiringItemsInterleaved(simStrands, rule, simStart,
                        BoundaryAfter(boundStarts, occurrenceStart, rule.Priority), minStartRemain, clipGrace,
                        ageRule);
                    // A real prefix of the group suffices — the remainder is checked recursively
                    // against strictly later occurrences (merged multi-occurrence chains).
                    var consumed = picks.Picks.Count - p;
                    if (consumed < 1 || consumed > rest)
                    {
                        context.NoteFailure(AiringInvalidReason.SlotSequenceMismatch);
                        continue;
                    }

                    var start = simStart;
                    var matches = true;
                    var pos = 0;
                    foreach (var pk in picks.Picks)
                    {
                        if (pos >= p)
                        {
                            var slot = airing[offset + pos - p];
                            if (!SlotMatchesItem(slot, pk.Item, start))
                            {
                                matches = false;
                                break;
                            }
                            start = slot.End;
                        }
                        else
                        {
                            start += pk.Item.Duration;
                        }
                        pos++;
                    }
                    if (!matches || pos < picks.Picks.Count)
                    {
                        context.NoteFailure(AiringInvalidReason.SlotSequenceMismatch);
                        continue;
                    }
                    if (AiringReproducible(airing, offset + consumed, occurrenceStart, context))
                        return true;
                    context.NoteFailure(AiringInvalidReason.RemainderNotReproducible);
                }
            }
        }
        return false;
    }

    // Locates a slot's content inside the expanded list: by media item first (unique), then by the
    // content reference (movie / episode) for material whose media item changed — matched on the
    // item's own content fields so playlist entries (ContentType PlaylistEntry, not the slot's
    // MovieId/TVShowEpisodeId) resolve the same way as direct movie/episode material.
    private static int FindItemIndex(List<SchedulableItem> items, ProgramSlot slot)
    {
        var idx = items.FindIndex(i => i.MediaItemId == slot.MediaItemId);
        if (idx >= 0)
            return idx;
        if (slot.MovieId is { } movieId)
            return items.FindIndex(i => i.MovieId == movieId);
        if (slot.TVShowEpisodeId is { } episodeId)
            return items.FindIndex(i => i.TVShowEpisodeId == episodeId);
        return -1;
    }

    // Rewinds each strand's cursor to the predecessor of its earliest displaced content that is
    // still expandable, so the next occurrence re-airs it (air order without a jump). Only
    // rewinds when the strand cursor has already passed that position.
    private static void RewindCursor(RuleState state, List<ProgramSlot> displacedSlots, IReadOnlyList<ProgramRuleCursor> cursorRows)
    {
        // Displaced clip slots belong to the clip-group rings, not the title strands — the
        // clip-first classification keeps a trailer clip (which carries MovieId) from being
        // misattributed to a title ring through the content-reference fallback.
        var clipIds = ClipMediaItemIds(state);
        var titleSlots = displacedSlots.Where(s => !clipIds.Contains(s.MediaItemId)).ToList();
        var clipSlots = displacedSlots.Where(s => clipIds.Contains(s.MediaItemId)).ToList();

        foreach (var strand in state.Strands)
            RewindRing(strand, strand.Key, titleSlots, slot => FindItemIndex(strand.Items, slot), cursorRows);

        foreach (var clipGroup in state.ClipGroups)
            RewindRing(clipGroup.Ring, clipGroup.StrandKey, clipSlots,
                slot => clipGroup.Ring.Items.FindIndex(i => i.MediaItemId == slot.MediaItemId), cursorRows);
    }

    // Rewinds one ring's cursor to the predecessor of its earliest displaced slot resolvable in
    // the ring — shared by the title strands (content index) and the clip-group rings (media
    // item index). Only rewinds when the ring cursor has already passed that position; either
    // way the earliest resolvable displaced content decides and ends the walk.
    private static void RewindRing(
        StrandState ring, string? key, List<ProgramSlot> slots,
        Func<ProgramSlot, int> indexOf, IReadOnlyList<ProgramRuleCursor> cursorRows)
    {
        var items = ring.Items;
        if (items.Count == 0)
            return;

        foreach (var slot in slots.OrderBy(s => s.Start))
        {
            var idx = indexOf(slot);
            if (idx < 0)
                continue;

            if (ring.NextIndex > idx)
            {
                var predecessor = items[(idx - 1 + items.Count) % items.Count];
                var cursor = SelectStrandCursor(cursorRows, key);
                if (cursor is not null)
                {
                    cursor.LastContentType = predecessor.ContentType;
                    cursor.LastContentId = predecessor.ContentId;
                    cursor.UpdatedAt = DateTime.UtcNow;
                }
                ring.RewindTo(idx);
            }
            break;
        }
    }

    // State derived from the loaded slot window of a reconcile run. Reload() is the only way to
    // (re-)set the loaded window: it re-derives every structure the validation and the
    // displacement/materialization phases read from the fresh Scheduled list (Rebuild) and
    // immediately re-runs the read-only validation over them (Validate — no slot status
    // touched), so after a widened reload nothing from a narrower window — not even a stale
    // validation result — can be reused by mistake.
    private sealed class ReconcileWindow(
        DateTime coverageStart,
        DateTimeOffset horizonEnd,
        string? federalState,
        List<ProgramRule> activeRules,
        Dictionary<long, RuleState> states,
        Dictionary<long, ProgramRule> rulesById,
        TimeSpan minStartRemain,
        ProgramScheduleAgeWindowRule ageRule,
        ILogger logger)
    {
        public List<ProgramSlot> Scheduled { get; private set; } = [];

        public List<Occurrence> Occurrences { get; private set; } = [];
        public List<(DateTime StartUtc, int Priority)> BoundStarts { get; private set; } = [];
        public List<List<ProgramSlot>> Airings { get; private set; } = [];
        public List<ProgramSlot> FillerSlots { get; private set; } = [];
        public List<(List<List<ProgramSlot>> Ordered, int FirstInvalid)> InvalidGroups { get; private set; } = [];
        public bool HasRescuableInvalid { get; private set; }

        private List<Interval> _occupiedEvidence = [];
        private Dictionary<long, HashSet<DateTime>> _occurrenceStarts = [];

        // Sets the load-window edge together with the freshly loaded slots and atomically
        // re-derives (Rebuild) and re-validates (Validate) — a reload can never leave stale
        // derived state or validation results behind. Displaced rows join solely as invader
        // evidence: the historical occupancy that legitimately shifted an airing stays provable
        // after the invader itself is displaced — it never counts as coverage.
        public void Reload(DateTime loadFrom, List<ProgramSlot> scheduled, List<ProgramSlot> displacedEvidence)
        {
            Scheduled = scheduled;
            Rebuild(loadFrom, displacedEvidence);
            Validate();
        }

        private void Rebuild(DateTime loadFrom, List<ProgramSlot> displacedEvidence)
        {
            // Invader evidence for the reproducibility check: an airing that starts later than
            // its nominal occurrence start is only plausible when a chain of occupied intervals
            // covered that start and ends exactly at the real start. Displaced rows contribute
            // the same evidence: the shift was proven at materialization time and stays
            // provable after the invader itself is displaced.
            _occupiedEvidence = Scheduled
                .Concat(displacedEvidence)
                .Select(s => new Interval(s.Start, s.End))
                .ToList();

            var reconcileLookback = coverageStart - loadFrom;
            Occurrences = BuildOccurrences(activeRules.Where(r => r.RuleType != ProgramRuleType.Filler), coverageStart, horizonEnd, federalState, reconcileLookback);
            _occurrenceStarts = Occurrences
                .GroupBy(o => o.Rule.Id)
                .ToDictionary(g => g.Key, g => g.Select(o => o.StartUtc).ToHashSet());
            BoundStarts = ProgramScheduleService.BoundStarts(Occurrences, states);

            // Non-filler slots group into airings (maximal contiguous runs of the same producing
            // rule); filler slots are validated individually.
            Airings = GroupAirings(Scheduled.Where(s => s.RuleType != ProgramRuleType.Filler).ToList());
            FillerSlots = Scheduled.Where(s => s.RuleType == ProgramRuleType.Filler).ToList();
        }

        private void Validate()
        {
            InvalidGroups = [];
            HasRescuableInvalid = false;
            foreach (var group in Airings.GroupBy(a => a[0].ProgramRuleId))
            {
                var ordered = group.OrderBy(a => a[0].Start).ToList();
                var firstInvalid = -1;
                for (var i = 0; i < ordered.Count && firstInvalid < 0; i++)
                {
                    var airing = ordered[i];
                    if (!airing.Any(s => s.End > coverageStart))
                        continue; // fully aired — history

                    var rule = airing[0].ProgramRuleId is { } rid ? rulesById.GetValueOrDefault(rid) : null;
                    AiringInvalidReason reason;
                    var invalid = true;
                    if (rule is null)
                    {
                        reason = AiringInvalidReason.RuleMissing;
                    }
                    else if (!rule.IsActive)
                    {
                        reason = AiringInvalidReason.RuleInactive;
                    }
                    else if (!_occurrenceStarts.TryGetValue(rule.Id, out var starts))
                    {
                        reason = AiringInvalidReason.NoOccurrenceAnchor;
                    }
                    else
                    {
                        invalid = !AiringReproducible(airing, states[rule.Id], starts, BoundStarts, _occupiedEvidence, minStartRemain, ageRule, out reason);
                    }

                    if (invalid)
                    {
                        firstInvalid = i;
                        HasRescuableInvalid |= rule is { IsActive: true };
                        logger.LogInformation(
                            "[ProgramSchedule] Ausstrahlung invalide: Regel {RuleId}, Kopf {Start:o}-{End:o}, {Slots} Sendeplaetze, Grund {Reason}.",
                            airing[0].ProgramRuleId, airing[0].Start, airing[0].End, airing.Count, reason);
                    }
                }

                if (firstInvalid >= 0)
                    InvalidGroups.Add((ordered, firstInvalid));
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
        Func<ProgramRule, ProgramSlot, bool> evicts,
        ProgramScheduleAgeWindowRule ageRule)
    {
        var state = states[occurrence.Rule.Id];
        if (state.Items.Count == 0)
            return null;

        var airingStart = ShiftPastInvaders(occurrence.StartUtc, blocked);

        var selection = SelectItems(state, occurrence.Rule, airingStart, bound, ageRule);
        var taken = selection.Taken;
        summary.BlockedByAgeWindow += selection.BlockedByAgeWindow;
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
        CommitTaken(state, selection);
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
        string StrandKey = "",
        int? AgeRatingMinAge = null)
    {
        // Every probed media file linked to the same content — the slot comparison accepts any
        // of them: a representative switch alone changes nothing about the airing.
        public IReadOnlyList<MediaItemVariant> MediaItemAlternates { get; init; } = [];
    }

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

        // Ordered strands hold air-order material (series episodes, playlist entries): their
        // head may never be reordered, so a window-blocked head defers instead of being
        // skipped. Unordered rings (movies, "other" genre material, clips) may skip. Computed
        // once — the item list never changes for a strand's lifetime, while the selection scan
        // and the tail checks read this per candidate.
        public bool PreservesOrder { get; } = items.Count > 0 && items.All(i => i.ContentType
            is ProgramCursorContentType.TVShowEpisode or ProgramCursorContentType.PlaylistEntry);

        public SchedulableItem? PeekAt(int offset)
            => Items.Count == 0 ? null : Items[(NextIndex + offset) % Items.Count];

        public void Commit(int count)
        {
            for (var i = 0; i < count; i++)
                LastPlaced = Items[(NextIndex + i) % Items.Count];
            NextIndex += count;
        }

        // Scan-based commit: NextIndex moves behind the last scanned position (window-skipped
        // positions stay consumed for the run), while LastPlaced must always be the last
        // actually taken item — pointing it at a skipped position would persist a never-aired
        // title and NextIndexFor would put it permanently behind the cursor.
        public void CommitScan(int scanned, SchedulableItem? lastTaken)
        {
            NextIndex += scanned;
            if (lastTaken is not null)
                LastPlaced = lastTaken;
        }

        public void RewindTo(int index) => NextIndex = index;
    }

    // Ring state of one clip group assigned to a rule: the group entity, its clip ring
    // (sequential draw, persisted via a "clipgroup:{id}" cursor strand) and whether it is the
    // reserved "Trailer" group whose items expand late from the freshly computed movie airings.
    private sealed class ClipGroupState
    {
        public ClipGroupState(ProgramClipGroup group, long ruleId)
        {
            Group = group;
            RuleId = ruleId;
            Ring = new StrandState(StrandKey, [], 0);
        }

        public ProgramClipGroup Group { get; }

        // Misch-Seed des Clip-Rings: Regeln mit derselben Clip-Gruppe erhalten so
        // unterschiedliche Ringordnungen.
        public long RuleId { get; }
        public bool IsTrailer => Group.Id == ProgramClipGroup.TrailerGroupId;
        public string StrandKey => $"clipgroup:{Group.Id}";
        public StrandState Ring { get; private set; }

        public void SetItems(List<SchedulableItem> items, IReadOnlyList<ProgramRuleCursor> cursors)
        {
            var ring = MixUnorderedRing(RuleId, StrandKey, items);
            Ring = new StrandState(StrandKey, ring, NextIndexFor(ring, SelectStrandCursor(cursors, StrandKey)));
        }
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

        // Clip-group rings live outside Strands — they are draw sources for the clip fill pass,
        // not airing material, and must not affect IsInterleaved, CommitTaken or the tail checks.
        public List<ClipGroupState> ClipGroups { get; } = [];
        public bool IsInterleaved => Strands.Count > 1;

        // The per-rule placeholder grace stops the title selection once the remaining period
        // drops below it — only when clip groups are assigned to a slot-duration rule;
        // otherwise the selection behaves exactly like before (no grace).
        public TimeSpan? ClipGrace => ClipGroups.Count > 0 && Rule.SlotDuration is not null
            ? Rule.PlaceholderGrace
            : null;

        // Flat-mode surface (single strand): used by filler handling and the flat airing
        // reproducibility check — identical semantics to the former single ring.
        public int NextIndex => Strands[0].NextIndex;
        public SchedulableItem? LastPlaced => Strands[0].LastPlaced;
        public bool PreservesOrder => Strands[0].PreservesOrder;
        public SchedulableItem? PeekAt(int offset) => Strands[0].PeekAt(offset);
        public void Commit(int count) => Strands[0].Commit(count);
        public void CommitScan(int scanned, SchedulableItem? lastTaken) => Strands[0].CommitScan(scanned, lastTaken);
        public void RewindTo(int index) => Strands[0].RewindTo(index);

        // Rebuilds items and strands — used when a rule's expansion refreshes mid-run
        // (trailer material expands only after the occurrences are materialized).
        public void SetItems(List<SchedulableItem> items, IReadOnlyList<ProgramRuleCursor> cursors)
        {
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
                    // Innerhalb der Stränge mischen dieselben Schluessel die ungeordneten
                    // Ringe ("other"-Filme); show:-Stränge bleiben in Sendedreihenfolge.
                    // Items bleibt die Expansionsliste — dort findet nur die
                    // Mitgliedschaftspruefung der Reproduzierbarkeit statt.
                    Items = items;
                    Strands = order
                        .OrderBy(key => StrandOrderKey(Rule.Id, key))
                        .Select(key =>
                        {
                            var ring = MixUnorderedRing(Rule.Id, key, byKey[key]);
                            return new StrandState(key, ring, NextIndexFor(ring, FindCursor(cursors, key)));
                        })
                        .ToList();
                    return;
                }
            }

            // Flachpfad: Items und der einzige Strang teilen dieselbe Ringinstanz —
            // SelectAiringItems und der flache Tail-Check lesen positionsbasiert ueber
            // state.Items.
            var flat = MixUnorderedRing(Rule.Id, null, items);
            Items = flat;
            Strands = [new StrandState(null, flat, NextIndexFor(flat, FindCursor(cursors, null)))];
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
        => Fnv1aHash($"{ruleId}:{strandKey}");

    // Stabiler, prozess-invarianter Ordnungsschluessel je Ringelement ungeordneter Ringe:
    // FNV-1a ueber Regel-Id, Strang-Schluessel und Inhaltsschluessel (gleiches Muster wie
    // StrandOrderKey). Regel- und Strangabhaengigkeit streut die Ringkoepfe ueber die
    // Regeln — ohne sie startete jede Materialmenge am selben Sortier-Ersten
    // (Alphabet/Id/Releasedatum) und bevorzugte ihn systematisch.
    internal static int ItemOrderKey(long ruleId, string? strandKey, ProgramCursorContentType contentType, long contentId)
        => Fnv1aHash($"{ruleId}:{strandKey}:{contentType}:{contentId}");

    private static int Fnv1aHash(string input)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes(input))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return (int)(hash & 0x7fffffff);
        }
    }

    // Ungeordnete Ringe tragen ausschliesslich Inhalt ohne fachliche Abspielreihenfolge
    // (Filme, Clip-MediaItems) — das Komplement der PreservesOrder-Stränge (Episoden,
    // Playlist-Eintraege). Der Trailer-Ring ist weder das eine noch das andere: seine
    // Airings-Reihenfolge (Vorlauf-Relevanz) bleibt erhalten.
    private static bool IsUnorderedRing(IReadOnlyList<SchedulableItem> items)
        => items.Count > 0 && items.All(i => i.ContentType
            is ProgramCursorContentType.Movie or ProgramCursorContentType.MediaItem);

    // Deterministische Mischung ungeordneter Ringe beim Ringaufbau: die Expansionen liefern
    // weiter ihre fachliche Sortierung, erst hier wird die Ringordnung per ItemOrderKey
    // gestreut. Geordnete und Trailer-Ringe gehen unveraendert durch.
    private static List<SchedulableItem> MixUnorderedRing(long ruleId, string? strandKey, List<SchedulableItem> items)
        => IsUnorderedRing(items)
            ? items.OrderBy(i => ItemOrderKey(ruleId, strandKey, i.ContentType, i.ContentId))
                .ThenBy(i => i.ContentId)
                .ToList()
            : items;

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
        public int BlockedByAgeWindow;

        // Diagnosis detail counters — only consumed by GetRuleDiagnosesAsync; the public run
        // and reconcile summaries are unchanged.
        public int Candidates;
        public int MaterialMissing;
        public int NoMediaItem;
        public int ProbePending;
        public int ProbeExhausted;

        public ProgramScheduleRunSummary ToSummary()
            => new(SlotsCreated, FillerSlotsCreated, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds, BlockedByAgeWindow);
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

    private static AccessContext EverythingContext()
        => new()
        {
            Everything = true,
            FullySharedSourceIds = [],
            UnlockedShowIdsForAll = [],
            UnlockedCollectionIdsForAll = []
        };

    private async Task<AccessContext> LoadAccessContextAsync(CancellationToken cancellationToken)
    {
        // The "Alle Inhalte" scope lifts the audience-release check entirely — the same
        // Everything context the "no non-admin users" branch below produces, and it spares
        // the three audience queries.
        if (await _settings.GetProgramScheduleContentScopeAsync(cancellationToken) == ProgramScheduleContentScope.AllContent)
        {
            return EverythingContext();
        }

        var userIds = await _db.Users.Where(u => !u.IsAdmin).Select(u => u.Id).ToListAsync(cancellationToken);
        if (userIds.Count == 0)
        {
            return EverythingContext();
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

    /// <summary>
    /// Loads the channel-wide youth-protection rule ("Jugendschutz-Regelung") of the program
    /// schedule — the configured window schema plus the behavior for material without a
    /// known rating.
    /// </summary>
    private async Task<ProgramScheduleAgeWindowRule> LoadAgeRuleAsync(CancellationToken cancellationToken)
        => new(
            await _settings.GetProgramScheduleAgeWindowsAsync(cancellationToken),
            await _settings.GetProgramScheduleUnknownRatingModeAsync(cancellationToken));

    // Index of the element right after the cursor position; 0 when there is no cursor or the
    // cursor element no longer exists in the expanded list.
    private static int NextIndexFor(List<SchedulableItem> items, ProgramRuleCursor? cursor)
    {
        if (cursor is null || items.Count == 0)
            return 0;

        var pos = items.FindIndex(i => i.ContentType == cursor.LastContentType && i.ContentId == cursor.LastContentId);
        return pos < 0 ? 0 : pos + 1;
    }

    private static List<Occurrence> BuildOccurrences(IEnumerable<ProgramRule> rules, DateTime coverageStartUtc, DateTimeOffset horizonEnd, string? federalState, TimeSpan lookback)
    {
        var tz = TimeZoneInfo.Local;
        var windowStartDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(coverageStartUtc - lookback, tz));
        var windowEndDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(horizonEnd.UtcDateTime, tz));

        var occurrences = new List<Occurrence>();
        foreach (var rule in rules)
        {
            for (var day = windowStartDay; day <= windowEndDay; day = day.AddDays(1))
            {
                // The validity window bounds the days the recurrence may fire on at all —
                // a day outside it produces no occurrence (Unrestricted never blocks).
                if (!ProgramRuleValidity.IsValidOn(rule, day))
                    continue;

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
                if (startUtc >= coverageStartUtc - lookback && startUtc < horizonEnd.UtcDateTime)
                    occurrences.Add(new Occurrence(rule, startUtc));
            }
        }

        return occurrences;
    }

    private void MaterializeOccurrence(Occurrence occurrence, Dictionary<long, RuleState> states, List<Interval> blocked, List<ProgramSlot> newSlots, DateTime coverageStart, (DateTime StartUtc, bool Hard)? bound, SummaryCounters summary, ProgramScheduleAgeWindowRule ageRule)
    {
        var state = states[occurrence.Rule.Id];
        if (state.Items.Count == 0)
            return;

        // A slot already occupying the occurrence start is a begun airing's overshooting tail —
        // it always airs to the end, so this airing starts behind it ("verschiebt sich").
        var airingStart = ShiftPastInvaders(occurrence.StartUtc, blocked);

        var selection = SelectItems(state, occurrence.Rule, airingStart, bound, ageRule);
        var taken = selection.Taken;
        summary.BlockedByAgeWindow += selection.BlockedByAgeWindow;
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
        CommitTaken(state, selection);
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
    // The outcome of one airing selection: the taken items in pick order plus, per strand, how
    // many ring positions the scan consumed (taken plus window-skipped) and how many candidates
    // the age window blocked. Scanned positions stay consumed for the run — a window-skipped
    // title is re-offered on the next ring lap against the then-current air time. The persisted
    // cursor follows LastPlaced, which CommitTaken sets to the last actually taken item only.
    private sealed record SelectionResult(
        List<SchedulableItem> Taken,
        List<StrandPick>? Picks,
        int[] Scanned,
        int BlockedByAgeWindow);

    // One pick of the interleaved selection: the strand and the taken item. The per-strand
    // scan extent (taken plus window-skipped positions) comes back in
    // <see cref="InterleavedSelection.Scanned"/> — the reproducibility check reconstructs the
    // ring positions itself from that scan trace and the slot sequence.
    private sealed record StrandPick(int Strand, SchedulableItem Item);

    // The outcome of the flat selection: taken items, each pick's ring position (scan trace),
    // the consumed scan positions and the window-blocked candidate count.
    private sealed record FlatSelection(List<SchedulableItem> Taken, int[] Positions, int Scanned, int BlockedByAgeWindow);

    // The outcome of the interleaved selection: picks, per-strand consumed scan positions and
    // the window-blocked candidate count.
    private sealed record InterleavedSelection(List<StrandPick> Picks, int[] Scanned, int BlockedByAgeWindow);

    private SelectionResult SelectItems(
        RuleState state, ProgramRule rule, DateTime airingStartUtc, (DateTime StartUtc, bool Hard)? bound,
        ProgramScheduleAgeWindowRule ageRule)
    {
        var clipGrace = state.ClipGrace;

        if (!state.IsInterleaved)
        {
            var flat = SelectAiringItems(state.Items, state.NextIndex, rule, airingStartUtc, bound, _minStartRemain, clipGrace, state.PreservesOrder, ageRule);
            return new SelectionResult(flat.Taken, null, [flat.Scanned], flat.BlockedByAgeWindow);
        }

        var interleaved = SelectAiringItemsInterleaved(state.Strands, rule, airingStartUtc, bound, _minStartRemain, clipGrace, ageRule);
        var selected = new List<SchedulableItem>();
        foreach (var pick in interleaved.Picks)
            selected.Add(pick.Item);
        return new SelectionResult(selected, interleaved.Picks, interleaved.Scanned, interleaved.BlockedByAgeWindow);
    }

    // Commits a selection after the airing passed the conflict check: each strand's cursor
    // moves behind its last scanned position while LastPlaced follows the last taken item.
    private static void CommitTaken(RuleState state, SelectionResult selection)
    {
        if (selection.Picks is not null)
        {
            var lastTaken = new SchedulableItem?[state.Strands.Count];
            foreach (var pick in selection.Picks)
                lastTaken[pick.Strand] = pick.Item;
            for (var i = 0; i < state.Strands.Count; i++)
                state.Strands[i].CommitScan(selection.Scanned[i], lastTaken[i]);
        }
        else
        {
            state.CommitScan(selection.Scanned[0], selection.Taken.Count > 0 ? selection.Taken[^1] : null);
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
    private static InterleavedSelection SelectAiringItemsInterleaved(
        List<StrandState> strands, ProgramRule rule, DateTime airingStartUtc,
        (DateTime StartUtc, bool Hard)? bound, TimeSpan minStartRemain, TimeSpan? clipGrace,
        ProgramScheduleAgeWindowRule ageRule)
    {
        var wanted = rule.EpisodeCount is > 0 ? rule.EpisodeCount.Value
            : rule.SlotDuration is not null ? int.MaxValue : 1;
        var target = rule.EpisodeCount is > 0 ? (TimeSpan?)null : rule.SlotDuration;

        var m = strands.Count;
        var picks = new List<StrandPick>();
        var scanned = new int[m];
        var skipsInRow = new int[m];
        var done = new bool[m];
        var blockedByWindow = 0;
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
        var graceHit = false;
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
                if (clipGrace is { } grace && target is { } t && t - accumulated < grace)
                {
                    graceHit = true; // period remainder below the grace — the rest goes to clip fill
                    break;
                }

                var count = strand.Items.Count;
                while (true)
                {
                    var item = strand.Items[(strand.NextIndex + scanned[s]) % count];
                    if (!ProgramScheduleAgeWindow.IsAllowedAt(item.AgeRatingMinAge, slotStart, ageRule))
                    {
                        blockedByWindow++;
                        if (strand.PreservesOrder)
                        {
                            // Ordered strand: the blocked head rests for this occurrence —
                            // air order stays strict, the position is not consumed.
                            done[s] = true;
                            break;
                        }
                        scanned[s]++; // unordered ring: skip — position consumed without a take
                        if (++skipsInRow[s] >= count)
                        {
                            done[s] = true; // a full ring lap without an allowed item
                            break;
                        }
                        continue;
                    }
                    if (bound is { } b && (b.StartUtc - slotStart < minStartRemain || (b.Hard && slotStart + item.Duration > b.StartUtc)))
                    {
                        done[s] = true; // head may not start / cannot complete — the strand rests
                        break;
                    }
                    picks.Add(new StrandPick(s, item));
                    scanned[s]++;
                    skipsInRow[s] = 0;
                    selected++;
                    slotStart += item.Duration;
                    accumulated += item.Duration;
                    progressed = true;
                    break;
                }
                if (target is not null && accumulated >= target)
                    break;
            }
            if (graceHit || !progressed)
                break;
            noProgressLaps = accumulated > lapAccumulated ? 0 : noProgressLaps + 1;
            if (noProgressLaps > maxLap)
                break; // every strand cycled a full lap without gaining airtime — degenerate durations
        }
        return new InterleavedSelection(picks, scanned, blockedByWindow);
    }

    // Boundary ownership: every occurrence owns [StartUtc, next occurrence start of any
    // item-bearing rule). An item is placed whenever it can still START inside the window
    // (<paramref name="minStartRemain"/> before the boundary) — and it always airs to the end:
    // items are indivisible, so a started item may legitimately overshoot a soft boundary and
    // shifts the following airing behind its real end. Against a hard boundary (higher-priority
    // next occurrence) an item must additionally complete inside the window. An item that may
    // not start simply waits — it is never cut and never silently skipped: the cursor stays and
    // the next occurrence retries it.
    private static FlatSelection SelectAiringItems(
        List<SchedulableItem> items, int headIndex, ProgramRule rule, DateTime airingStartUtc,
        (DateTime StartUtc, bool Hard)? bound, TimeSpan minStartRemain, TimeSpan? clipGrace,
        bool preservesOrder, ProgramScheduleAgeWindowRule ageRule)
    {
        // EpisodeCount wins; without it SlotDuration bounds the airing; when neither is set the
        // airing gets exactly one item (rule validation normally requires one of both).
        var wanted = rule.EpisodeCount is > 0 ? rule.EpisodeCount.Value
            : rule.SlotDuration is not null ? int.MaxValue : 1;
        var target = rule.EpisodeCount is > 0 ? (TimeSpan?)null : rule.SlotDuration;

        var count = items.Count;
        var selected = new List<SchedulableItem>();
        var positions = new List<int>();
        var accumulated = TimeSpan.Zero;
        var slotStart = airingStartUtc;
        var ring = 0;            // consumed scan positions (taken + skipped)
        var blockedByWindow = 0;
        var sinceProgress = 0;   // scanned positions since the accumulated airtime last grew
        while (selected.Count < wanted)
        {
            var item = items[(headIndex + ring) % count];
            if (!ProgramScheduleAgeWindow.IsAllowedAt(item.AgeRatingMinAge, slotStart, ageRule))
            {
                // Blocked at this slot's local start: an ordered strand keeps its head waiting
                // (air order stays strict); an unordered ring skips the title — the scanned
                // position stays consumed without a take.
                blockedByWindow++;
                if (preservesOrder)
                    break;
                ring++;
                if (++sinceProgress >= count)
                    break; // a full ring lap without an allowed item
                continue;
            }
            if (clipGrace is { } grace && target is { } t && t - accumulated < grace)
                break; // period remainder below the grace — the rest goes to clip fill
            if (bound is { } b)
            {
                if (b.StartUtc - slotStart < minStartRemain)
                    break; // not enough room left to start — the rest goes to fillers
                if (b.Hard && slotStart + item.Duration > b.StartUtc)
                    break; // higher-priority window — the item cannot complete, defers
            }

            selected.Add(item);
            positions.Add((headIndex + ring) % count);
            ring++;
            slotStart += item.Duration;
            accumulated += item.Duration;
            sinceProgress = item.Duration > TimeSpan.Zero ? 0 : sinceProgress + 1;
            if (sinceProgress >= count)
                break; // a full ring gained no airtime — degenerate (zero) durations
            if (target is not null && accumulated >= target)
                break;
        }
        return new FlatSelection(selected, positions.ToArray(), ring, blockedByWindow);
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
        ProgramScheduleAgeWindowRule ageRule,
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
                    // Filler rules have no occurrences — their validity window is checked per
                    // gap position: a filler outside its window does not fill. The day is taken
                    // from the current position — fillAt may cross local midnight within a round.
                    var fillDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fillAt, TimeZoneInfo.Local));
                    if (!ProgramRuleValidity.IsValidOn(filler.Rule, fillDay))
                        continue;
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
                        if (item is not null
                            && !ProgramScheduleAgeWindow.IsAllowedAt(item.AgeRatingMinAge, fillAt, ageRule))
                            continue;
                    }
                    else
                    {
                        // Age window: scan the filler ring for the first item allowed at this
                        // position — the positions passed over stay consumed once a take
                        // commits the scan (take covers them).
                        item = null;
                        take = 0;
                        for (var o = 0; o < filler.Items.Count; o++)
                        {
                            var candidate = filler.PeekAt(o);
                            if (candidate is not null
                                && ProgramScheduleAgeWindow.IsAllowedAt(candidate.AgeRatingMinAge, fillAt, ageRule))
                            {
                                item = candidate;
                                take = o + 1;
                                break;
                            }
                        }
                        if (item is null)
                            continue;
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

                // Ist an diesem lokalen Tag gar kein Füller gültig, wird der Rest der Lücke nicht
                // aufgegeben, sondern zum nächsten Tageswechsel gesprungen — ein wiederkehrendes
                // Fenster kann am Folgetag wieder offen sein. Der übersprungene Abschnitt bleibt
                // eine echte Lücke und fließt in die Restzeit-Summe.
                if (!progressed && fillAt < gap.End)
                {
                    var restDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fillAt, TimeZoneInfo.Local));
                    if (fillerStates.All(f => !ProgramRuleValidity.IsValidOn(f.Rule, restDay)))
                    {
                        var dayEnd = TimeZoneInfo.ConvertTimeToUtc(restDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local);
                        summary.RemainingGapSeconds += (long)(Math.Min(dayEnd.Ticks, gap.End.Ticks) - fillAt.Ticks) / TimeSpan.TicksPerSecond;
                        fillAt = dayEnd;
                        progressed = fillAt < gap.End;
                    }
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
            // Clip-group rings persist their draw position under "clipgroup:{groupId}" strands.
            foreach (var strand in state.Strands
                         .Concat(state.ClipGroups.Select(c => c.Ring))
                         .Where(s => s.LastPlaced is not null))
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

    private async Task<List<SchedulableItem>> ExpandMaterialAsync(ProgramRule rule, AccessContext access, SummaryCounters summary,
        IReadOnlyDictionary<long, List<DateTime>>? airingsByMovie, DateTime coverageStart, CancellationToken cancellationToken)
    {
        var items = rule.MaterialType switch
        {
            ProgramRuleMaterialType.TVShow => await ExpandShowAsync(rule.MaterialId, access, summary, cancellationToken),
            ProgramRuleMaterialType.Movie => await ExpandMovieAsync(rule.MaterialId, access, summary, cancellationToken),
            ProgramRuleMaterialType.MovieCollection => await ExpandMovieCollectionAsync(rule.MaterialId, access, summary, cancellationToken),
            ProgramRuleMaterialType.Genre => await ExpandGenreAsync(rule, access, summary, coverageStart, cancellationToken),
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

    // One probed alternative file of a content — the slot comparison checks the stored file's
    // own runtime, so the variant carries (id, duration) only.
    private sealed record MediaItemVariant(long MediaItemId, TimeSpan Duration);

    // Result of the per-content representative pick: the chosen link plus every probed
    // alternative file linked to the same content.
    private sealed record MediaItemChoice(MediaItemRef Representative, IReadOnlyList<MediaItemVariant> Alternates);

    // Shared projection shape of the per-content link queries below — the content id differs
    // (MovieId vs. TVShowEpisodeId), everything else feeds the identical choice grouping.
    private sealed record MediaItemLink(long ContentId, long MediaItemId, TimeSpan? Duration, DateTime? DurationProbedAt, int DurationProbeAttempts);

    private async Task<Dictionary<long, MediaItemChoice>> MovieMediaItemsAsync(IReadOnlyCollection<long> movieIds, CancellationToken cancellationToken)
    {
        var links = await _db.MovieMediaItems
            .Where(l => movieIds.Contains(l.MovieId) && !l.IsTrailer)
            .Select(l => new MediaItemLink(l.MovieId, l.MediaItemId, l.MediaItem.Duration, l.MediaItem.DurationProbedAt, l.MediaItem.DurationProbeAttempts))
            .ToListAsync(cancellationToken);
        return ToMediaItemChoices(links);
    }

    private async Task<Dictionary<long, MediaItemChoice>> EpisodeMediaItemsAsync(IReadOnlyCollection<long> episodeIds, CancellationToken cancellationToken)
    {
        var links = await _db.TVShowEpisodeMediaItems
            .Where(l => episodeIds.Contains(l.TVShowEpisodeId))
            .Select(l => new MediaItemLink(l.TVShowEpisodeId, l.MediaItemId, l.MediaItem.Duration, l.MediaItem.DurationProbedAt, l.MediaItem.DurationProbeAttempts))
            .ToListAsync(cancellationToken);
        return ToMediaItemChoices(links);
    }

    // Groups the links per content into the representative pick (probed duration first, ties by
    // lowest MediaItemId) plus the alternates list of every probed link.
    private static Dictionary<long, MediaItemChoice> ToMediaItemChoices(List<MediaItemLink> links)
        => links.GroupBy(l => l.ContentId).ToDictionary(
            g => g.Key,
            g => new MediaItemChoice(
                g.OrderBy(l => l.Duration is null ? 1 : 0).ThenBy(l => l.MediaItemId)
                    .Select(l => new MediaItemRef(l.MediaItemId, l.Duration, l.DurationProbedAt, l.DurationProbeAttempts))
                    .First(),
                g.Where(l => l.Duration is not null)
                    .Select(l => new MediaItemVariant(l.MediaItemId, l.Duration!.Value))
                    .ToList()));

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

    private SchedulableItem? TryMovieItem(Movie movie, Dictionary<long, MediaItemChoice> mediaItems, AccessContext access, SummaryCounters summary)
    {
        summary.Candidates++;
        mediaItems.TryGetValue(movie.Id, out var choice);
        var media = choice?.Representative;
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
        return new SchedulableItem(ProgramCursorContentType.Movie, movie.Id, media.MediaItemId, movie.Id, null, media.Duration.Value, AgeRatingMinAge: movie.AgeRatingMinAge)
        {
            MediaItemAlternates = choice!.Alternates
        };
    }

    private async Task<List<SchedulableItem>> ExpandShowAsync(long showId, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        var showRating = await _db.TVShows
            .Where(s => s.Id == showId)
            .Select(s => new { s.AgeRatingMinAge })
            .FirstOrDefaultAsync(cancellationToken);
        if (showRating is null)
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
            .Select(e => ToItem(new ProxyEpisode(e.Id, e.MediaSourceId), showId, mediaItems, access, summary, showRating.AgeRatingMinAge))
            .Where(i => i is not null)
            .Cast<SchedulableItem>()
            .ToList();
    }

    // Minimal carrier for episode fields needed by ToItem (id + source).
    private sealed record ProxyEpisode(long Id, long MediaSourceId);

    private SchedulableItem? ToItem(ProxyEpisode episode, long showId, Dictionary<long, MediaItemChoice> mediaItems, AccessContext access, SummaryCounters summary, int? ageRatingMinAge)
    {
        summary.Candidates++;
        mediaItems.TryGetValue(episode.Id, out var choice);
        var media = choice?.Representative;
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
        return new SchedulableItem(ProgramCursorContentType.TVShowEpisode, episode.Id, media.MediaItemId, null, episode.Id, media.Duration.Value, AgeRatingMinAge: ageRatingMinAge)
        {
            MediaItemAlternates = choice!.Alternates
        };
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
    // ProgramRuleGenre rows carry two meanings: a positive MaterialId is the single-genre
    // fallback (legacy data, old backups), while MaterialId 0 is the persisted „keine Auswahl =
    // alle Genres" marker and expands to every usable genre in scope.
    private async Task<List<SchedulableItem>> ExpandGenreAsync(ProgramRule rule, AccessContext access, SummaryCounters summary, DateTime coverageStart, CancellationToken cancellationToken)
    {
        var sourceFilter = rule.MediaSourceFilters.Select(s => s.MediaSourceId).ToHashSet();
        List<long> storedIds = rule.Genres.Count > 0
            ? rule.Genres.Select(g => g.GenreId).ToList()
            : rule.MaterialId > 0 ? [rule.MaterialId] : [];

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
        if (storedGenres.Count == 0 && storedIds.Count > 0)
        {
            summary.MaterialMissing++;
            return [];
        }

        var allRows = scopedGenres
            .Concat(storedGenres.Where(s => scopedGenres.All(g => g.Id != s.Id)))
            .ToList();
        var canonicalNames = GenreCanonicalResolver.ResolveCanonicalNames(allRows, coverageStart);

        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stored in storedGenres)
            selectedNames.UnionWith(canonicalNames.GetValueOrDefault(stored.Id, []));
        if (storedIds.Count > 0 && selectedNames.Count == 0)
        {
            // Every stored genre is hidden/out-of-season without a usable alternate — the
            // selection resolves to nothing.
            summary.MaterialMissing++;
            return [];
        }

        // storedIds empty: „keine Auswahl = alle Genres" — every genre row in scope that resolves
        // to a usable canonical name participates; otherwise only rows overlapping the selection.
        var effectiveGenreIds = scopedGenres
            .Where(g => storedIds.Count == 0
                ? canonicalNames[g.Id].Count > 0
                : canonicalNames[g.Id].Overlaps(selectedNames))
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
        var showMinAges = await _db.TVShows
            .Where(s => showIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.AgeRatingMinAge, cancellationToken);

        // Strand keys drive the series interleaving (N19): every show is its own strand so its
        // episodes continue in air order at a stable slot position; movies share the "other"
        // strand and keep flowing through it in release order like a series of their own.
        var movieItems = movies
            .Select(m => TryMovieItem(m, mediaByMovie, access, summary))
            .Where(i => i is not null)
            .Cast<SchedulableItem>()
            .Select(i => i with { StrandKey = OtherStrandKey });
        var episodeItems = episodes
            .Select(e => (Item: ToItem(new ProxyEpisode(e.Id, e.MediaSourceId), e.ShowId, mediaByEpisode, access, summary, showMinAges.GetValueOrDefault(e.ShowId)), e.ShowId))
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
        => FindLeadMatchOffset(state.Strands[0], fillAt, leadEnd, airingsByMovie);

    private static int? FindLeadMatchOffset(StrandState ring, DateTime fillAt, DateTime leadEnd, IReadOnlyDictionary<long, List<DateTime>> airingsByMovie)
    {
        for (var i = 0; i < ring.Items.Count; i++)
        {
            var item = ring.PeekAt(i);
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
            .Select(mi => new ClipCandidate(mi.Id, mi.Path, mi.MediaCollection.MediaSourceId, mi.Duration, mi.DurationProbedAt, mi.DurationProbeAttempts))
            .ToListAsync(cancellationToken);

        return await FilterClipCandidatesAsync(items, access, summary, cancellationToken);
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
        var showMinAges = await _db.TVShows
            .Where(s => episodeShowIds.Values.Select(e => e.ShowId).Distinct().Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.AgeRatingMinAge, cancellationToken);

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
                var item = ToItem(new ProxyEpisode(episode.Id, episode.MediaSourceId), episode.ShowId, mediaByEpisode, access, summary, showMinAges.GetValueOrDefault(episode.ShowId));
                if (item is not null)
                    items.Add(item with { ContentType = ProgramCursorContentType.PlaylistEntry, ContentId = entry.Id });
            }
            // Orphaned entries (referenced media no longer exists) are skipped silently.
        }
        return items;
    }

    // ---- clip-group fill ----

    // Shared context of the clip-group build/expansion/fill passes — bundled like ReproContext
    // so the helpers keep short signatures: the audience snapshot, the movie-airing map for the
    // trailer group's lead matching (null while a run has not computed it yet), the coverage
    // edge, the reserved occurrence starts for the free-room check, the horizon cap and the
    // run counters.
    private sealed record ClipFillContext(
        AccessContext Access,
        IReadOnlyDictionary<long, List<DateTime>>? AiringsByMovie,
        DateTime CoverageStart,
        IReadOnlyList<(DateTime StartUtc, int Priority)> BoundStarts,
        DateTime HorizonEnd,
        SummaryCounters Summary,
        ProgramScheduleAgeWindowRule AgeRule,
        // Nominale Occurrence-Starts je Regel (aufsteigend) — die Grundlage des Periodenendes.
        // Die Ausstrahlung kann real spaeter beginnen (invader shift) oder der geschriebene
        // Kopf mitten in der Periode liegen; das Periodenende darf sich dadurch nie nach
        // hinten verschieben.
        IReadOnlyDictionary<long, IReadOnlyList<DateTime>>? OccurrenceStarts = null)
    {
        // Per-rule round-robin draw position of the fill pass — mutable run state shared by
        // the `with`-clones of this record, so the draw continues across airings of a rule.
        public Dictionary<long, int> RoundRobin { get; } = new();
    }

    // A clip-ring candidate row of either clip expansion: the media item plus the fields the
    // schedulability filter needs (video check, classification links, duration probe state,
    // owning source for the audience check).
    private sealed record ClipCandidate(
        long MediaItemId, string Path, long MediaSourceId,
        TimeSpan? Duration, DateTime? DurationProbedAt, int DurationProbeAttempts);

    // Builds the per-group ring states of a rule's clip-group assignment, ordered by
    // ProgramClipGroupId (the round-robin order of the fill pass). User groups expand
    // immediately through ExpandClipGroupItemsAsync; the reserved "Trailer" group only expands
    // when the movie-airing map is already known (reconcile) — during extend-only runs it stays
    // empty and is filled later by ExpandClipGroupStatesAsync, once the freshly materialized
    // movie airings of this run exist. A link whose group row is missing (old backup without
    // the seed row) yields no state — tolerated per design.
    private async Task<List<ClipGroupState>> BuildClipGroupStates(
        ProgramRule rule, IReadOnlyList<ProgramRuleCursor> cursors, ClipFillContext ctx, CancellationToken cancellationToken)
    {
        var result = new List<ClipGroupState>();
        foreach (var link in rule.ClipGroups.OrderBy(c => c.ProgramClipGroupId))
        {
            if (link.ClipGroup is null)
                continue;

            var clipState = new ClipGroupState(link.ClipGroup, rule.Id);
            if (clipState.IsTrailer)
            {
                if (ctx.AiringsByMovie is not null)
                    clipState.SetItems(await ExpandTrailersAsync(ctx.Access, ctx.AiringsByMovie, ctx.CoverageStart, ctx.Summary, cancellationToken), cursors);
            }
            else
            {
                clipState.SetItems(await ExpandClipGroupItemsAsync(link.ClipGroup.Id, ctx.Access, ctx.Summary, cancellationToken), cursors);
            }
            result.Add(clipState);
        }
        return result;
    }

    // Deferred expansion pass for the reserved "Trailer" clip group: runs after the occurrences
    // are materialized so freshly scheduled movie airings of this run count — same reason the
    // trailer material expands late (see ExpandTrailerStatesAsync).
    private async Task ExpandClipGroupStatesAsync(
        Dictionary<long, RuleState> states, Dictionary<long, IReadOnlyList<ProgramRuleCursor>> cursors,
        ClipFillContext ctx, CancellationToken cancellationToken)
    {
        var airingsByMovie = ctx.AiringsByMovie ?? new Dictionary<long, List<DateTime>>();
        foreach (var state in states.Values)
        {
            var ruleCursors = cursors.GetValueOrDefault(state.Rule.Id) ?? [];
            foreach (var clipState in state.ClipGroups.Where(c => c.IsTrailer && c.Ring.Items.Count == 0))
                clipState.SetItems(await ExpandTrailersAsync(ctx.Access, airingsByMovie, ctx.CoverageStart, ctx.Summary, cancellationToken), ruleCursors);
        }
    }

    // Expands a user clip group to its schedulable member ring: media items that are still
    // unclassified videos (the classification may have changed since the assignment), carry a
    // probed duration and are released for the whole audience — the same "clip" definition as
    // ExpandMediaSourceClipsAsync, but across sources. Ordered by media item id for a stable ring.
    private async Task<List<SchedulableItem>> ExpandClipGroupItemsAsync(
        long groupId, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        var items = await _db.ProgramClipGroupItems
            .AsNoTracking()
            .Where(i => i.ProgramClipGroupId == groupId)
            .Select(i => new ClipCandidate(
                i.MediaItemId,
                i.MediaItem.Path,
                i.MediaItem.MediaCollection.MediaSourceId,
                i.MediaItem.Duration,
                i.MediaItem.DurationProbedAt,
                i.MediaItem.DurationProbeAttempts))
            .ToListAsync(cancellationToken);

        return await FilterClipCandidatesAsync(items, access, summary, cancellationToken);
    }

    // Shared filter pipeline of the clip expansions (clip-source material and clip-group
    // members): only unclassified video files with a probed duration and a source released for
    // the whole audience become schedulable — ordered by media item id for a stable ring.
    private async Task<List<SchedulableItem>> FilterClipCandidatesAsync(
        List<ClipCandidate> candidates, AccessContext access, SummaryCounters summary, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
            return [];

        var ids = candidates.Select(c => c.MediaItemId).ToList();
        var linkedMovieItems = (await _db.MovieMediaItems
                .Where(l => ids.Contains(l.MediaItemId))
                .Select(l => l.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var linkedEpisodeItems = (await _db.TVShowEpisodeMediaItems
                .Where(l => ids.Contains(l.MediaItemId))
                .Select(l => l.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var result = new List<SchedulableItem>();
        foreach (var item in candidates.OrderBy(i => i.MediaItemId))
        {
            if (!Utils.VideoFileExtensions.IsVideoFileName(item.Path))
                continue;
            if (linkedMovieItems.Contains(item.MediaItemId) || linkedEpisodeItems.Contains(item.MediaItemId))
                continue; // classified content is regular material, not filler clips
            summary.Candidates++;
            if (item.Duration is null)
            {
                CountNoDuration(new MediaItemRef(item.MediaItemId, item.Duration, item.DurationProbedAt, item.DurationProbeAttempts), summary);
                continue;
            }
            if (!access.Allows(item.MediaSourceId, null, null))
            {
                summary.SkippedNoAccess++;
                continue;
            }
            result.Add(new SchedulableItem(ProgramCursorContentType.MediaItem, item.MediaItemId, item.MediaItemId, null, null, item.Duration.Value));
        }
        return result;
    }

    // Sequential round-robin draw over the clip-group rings: the next group whose offered item
    // satisfies the placement predicate supplies the clip. User groups offer their ring head;
    // the reserved trailer group offers its head-of-lead-match item (FindLeadMatchOffset
    // semantics at the position) or nothing. Groups that cannot contribute are skipped without
    // consuming their ring — no fitting search over items.
    private SchedulableItem? NextClipItem(
        List<ClipGroupState> clipGroups, ref int roundRobin, DateTime position,
        IReadOnlyDictionary<long, List<DateTime>> airingsByMovie,
        Func<SchedulableItem, bool> fits)
    {
        var count = clipGroups.Count;
        for (var t = 0; t < count; t++)
        {
            var index = (roundRobin + t) % count;
            var group = clipGroups[index];
            var ring = group.Ring;
            if (ring.Items.Count == 0)
                continue;

            int take;
            SchedulableItem? item;
            if (group.IsTrailer)
            {
                var offset = FindLeadMatchOffset(ring, position, position + _trailerLead, airingsByMovie);
                if (offset is null)
                    continue;
                take = offset.Value + 1;
                item = ring.PeekAt(offset.Value);
            }
            else
            {
                take = 1;
                item = ring.PeekAt(0);
            }

            if (item is null || !fits(item))
                continue;

            ring.Commit(take);
            roundRobin = (index + 1) % count;
            return item;
        }
        return null;
    }

    // Post-pass after materialization: distributes clip slots of the assigned clip groups into
    // the n+1 segments of each slot-duration airing — the head segment before the first title
    // included — whenever a positive rest remains between the last title's end and the period
    // end (firstSlot.Start + SlotDuration). The rest is shared evenly across the segments with
    // leftover budget carried forward; clips are drawn sequentially round-robin over the group
    // rings. Clips only go into free space: no overlap with occupied intervals and no overlap
    // with reserved bound windows of higher-priority rules. The last clip may overshoot the
    // period end while the space stays free; an overshooting clip reaching into occupied or
    // bound space is dropped and the remainder stays an open gap for FillGaps. Occupied
    // neighbors are never shifted. After the pass, blocked reflects the final geometry.
    private void InsertClipFill(
        List<ProgramSlot> newSlots,
        List<Interval> blocked,
        Dictionary<long, RuleState> states,
        ClipFillContext ctx)
    {
        var airings = GroupAirings(newSlots.OrderBy(s => s.Start).ToList());
        var rebuilt = new List<ProgramSlot>(newSlots.Count);

        foreach (var airing in airings)
        {
            var state = airing[0].ProgramRuleId is { } ruleId ? states.GetValueOrDefault(ruleId) : null;
            var rule = state?.Rule;
            if (rule is null || state!.ClipGroups.Count == 0 || rule.SlotDuration is null)
            {
                rebuilt.AddRange(airing);
                continue;
            }

            FillAiringSegments(airing, state, blocked, rebuilt, ctx);
        }

        newSlots.Clear();
        newSlots.AddRange(rebuilt);
    }

    // The tail space an airing may grow into: nearest occupied start or reserved
    // higher-priority bound window after its end, capped at the horizon.
    private static DateTime TailLimit(
        DateTime lastEnd, int rulePriority, List<Interval> blocked,
        IReadOnlyList<(DateTime StartUtc, int Priority)> boundStarts, DateTime horizonEnd)
    {
        var limit = horizonEnd;
        foreach (var o in blocked)
            if (o.Start >= lastEnd && o.Start < limit)
                limit = o.Start;
        foreach (var b in boundStarts)
            if (b.Priority > rulePriority && b.StartUtc >= lastEnd && b.StartUtc < limit)
                limit = b.StartUtc;
        return limit;
    }

    // The space the whole re-laid chain (titles plus inserted clips) may span into: nearest
    // occupied start OR reserved occurrence-start bound of any priority after the airing's
    // current end — another occurrence owns its window regardless of priority, so a shifted
    // title must never reach into it; only a started clip may overshoot (TailLimit).
    private static DateTime ChainLimit(
        DateTime lastEnd, List<Interval> blocked,
        IReadOnlyList<(DateTime StartUtc, int Priority)> boundStarts, DateTime horizonEnd)
    {
        var limit = horizonEnd;
        foreach (var o in blocked)
            if (o.Start >= lastEnd && o.Start < limit)
                limit = o.Start;
        foreach (var b in boundStarts)
            if (b.StartUtc >= lastEnd && b.StartUtc < limit)
                limit = b.StartUtc;
        return limit;
    }

    // The nominal start of the period an airing belongs to: the latest occurrence start of
    // its rule at or before the airing's written head. The head can sit behind the nominal
    // start (invader shift) or mid-period (partially aired occurrence — slots ending before
    // the coverage start produce no rows). Null when no occurrence matches — the period end
    // is then undecidable and the airing must be left untouched.
    private static DateTime? PeriodStart(ClipFillContext ctx, long ruleId, DateTime headStartUtc)
    {
        if (ctx.OccurrenceStarts is null || !ctx.OccurrenceStarts.TryGetValue(ruleId, out var starts))
            return null;
        DateTime? result = null;
        foreach (var start in starts)
        {
            if (start > headStartUtc)
                break;
            result = start;
        }
        return result;
    }

    // Re-lays a single slot-duration airing with clip fill (the caller has already checked the
    // rule carries clip groups and a slot duration): nothing to do while no positive rest
    // remains between the last title's end and the period end. Otherwise the rest — capped at
    // the free room before the next occupied or reserved boundary — is shared evenly across
    // the n+1 segments (leftover budget carried forward), clips are drawn sequentially
    // round-robin over the group rings and the title slots are re-positioned behind them.
    // A tail clip must start before the period end and inside the owned window, and may
    // overshoot it while the space stays free — a clip reaching into occupied or bound space
    // is dropped. The method keeps blocked in sync itself: the airing's own span leaves the
    // occupied set before the re-lay and the rebuilt geometry is registered afterwards.
    private void FillAiringSegments(
        List<ProgramSlot> airing, RuleState state, List<Interval> blocked,
        List<ProgramSlot> rebuilt, ClipFillContext ctx)
    {
        var rule = state.Rule;
        var lastEnd = airing[^1].End;
        var periodStart = PeriodStart(ctx, rule.Id, airing[0].Start);
        if (periodStart is null)
        {
            rebuilt.AddRange(airing);
            return;
        }
        var periodEnd = periodStart.Value + rule.SlotDuration!.Value;
        var rest = periodEnd - lastEnd;
        if (rest <= TimeSpan.Zero)
        {
            rebuilt.AddRange(airing);
            return;
        }

        // The airing's own span leaves the occupied set — its slots are re-laid below.
        foreach (var slot in airing)
            blocked.RemoveAll(b => b.Start == slot.Start && b.End == slot.End);

        var tailLimit = TailLimit(lastEnd, rule.Priority, blocked, ctx.BoundStarts, ctx.HorizonEnd);
        var chainEnd = ChainLimit(lastEnd, blocked, ctx.BoundStarts, ctx.HorizonEnd);
        var fill = rest < chainEnd - lastEnd ? rest : chainEnd - lastEnd;
        var roundRobin = ctx.RoundRobin.GetValueOrDefault(rule.Id);
        var airingStartIndex = rebuilt.Count;
        var airingsByMovie = ctx.AiringsByMovie ?? new Dictionary<long, List<DateTime>>();
        var share = TimeSpan.FromTicks(fill.Ticks / (airing.Count + 1));
        var carry = TimeSpan.Zero;
        var pos = airing[0].Start;
        var now = DateTime.UtcNow;

        for (var i = 0; i <= airing.Count; i++)
        {
            var tail = i == airing.Count;
            var budget = share + carry;
            var placed = TimeSpan.Zero;

            while (true)
            {
                var clip = NextClipItem(state.ClipGroups, ref roundRobin, pos, airingsByMovie,
                    item => ProgramScheduleAgeWindow.IsAllowedAt(item.AgeRatingMinAge, pos, ctx.AgeRule)
                        && (tail
                            ? pos < periodEnd && pos < chainEnd
                                && pos + item.Duration <= (placed > TimeSpan.Zero
                                    ? tailLimit
                                    : periodEnd < tailLimit ? periodEnd : tailLimit)
                            : placed + item.Duration <= budget && pos < chainEnd && pos + item.Duration <= tailLimit));
                if (clip is null)
                    break;

                rebuilt.Add(new ProgramSlot
                {
                    TVChannelId = rule.TVChannelId,
                    Start = pos,
                    End = pos + clip.Duration,
                    MediaItemId = clip.MediaItemId,
                    MovieId = clip.MovieId,
                    TVShowEpisodeId = clip.TVShowEpisodeId,
                    ProgramRuleId = rule.Id,
                    RuleType = rule.RuleType,
                    IsClipFill = true,
                    CreatedAt = now
                });
                pos += clip.Duration;
                placed += clip.Duration;
                ctx.Summary.SlotsCreated++;
            }

            if (!tail)
                carry = budget - placed;

            if (i < airing.Count)
            {
                var title = airing[i];
                var duration = title.End - title.Start;
                title.Start = pos;
                title.End = pos + duration;
                pos = title.End;
                rebuilt.Add(title);
            }
        }

        ctx.RoundRobin[rule.Id] = roundRobin;

        for (var j = airingStartIndex; j < rebuilt.Count; j++)
            blocked.Add(new Interval(rebuilt[j].Start, rebuilt[j].End));
    }

    // Slot-vs-item match of the reproducibility checks: the slot's stored media item may be the
    // content's current representative or any of its still-linked probed alternates — a
    // representative switch alone changes nothing about the airing. The slot's length must
    // match the stored item's own runtime (a re-linked file with a different runtime is a real
    // change); an unlinked/deleted stored item matches nothing and the airing is displaced.
    private static bool SlotMatchesItem(ProgramSlot slot, SchedulableItem item, DateTime expectedStart)
    {
        if (slot.Start != expectedStart)
            return false;
        var duration = item.MediaItemId == slot.MediaItemId
            ? item.Duration
            : item.MediaItemAlternates.FirstOrDefault(a => a.MediaItemId == slot.MediaItemId)?.Duration;
        return duration is not null && slot.End == expectedStart + duration.Value;
    }
}
