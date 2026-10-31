using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Services;

/// <summary>Describes what one synchronization tick decided.</summary>
public enum LiveStreamSyncAction
{
    /// <summary>The running segmentation already matches the schedule — nothing changed.</summary>
    Unchanged,
    /// <summary>A segmentation job was (re)started for the current broadcast.</summary>
    Started,
    /// <summary>The segmentation was stopped (schedule gap or unavailable input).</summary>
    Stopped,
    /// <summary>ffmpeg could not be resolved — no segmentation possible.</summary>
    FfmpegUnavailable,
    /// <summary>The current broadcast's media could not be resolved to an input.</summary>
    MediaUnavailable,
    /// <summary>The current broadcast's media resolves, but its input proved undecodable
    /// for ffmpeg — the slot is skipped until its end instead of restart-looping.</summary>
    InputUnplayable
}

/// <summary>
/// Result of one <see cref="LiveStreamCoordinator.SynchronizeAsync"/> tick: what was decided and
/// when the next decision is due.
/// </summary>
/// <param name="Action">What the tick did.</param>
/// <param name="WakeAtUtc">When the schedule next requires a decision (slot end or next slot
/// start); <see langword="null"/> means "poll on the worker's regular cadence".</param>
/// <param name="Detail">Optional human-readable detail for logging.</param>
public sealed record LiveStreamSyncResult(LiveStreamSyncAction Action, DateTimeOffset? WakeAtUtc, string? Detail = null);

/// <summary>
/// Brings the running HLS segmentation in line with the broadcast schedule, purely time-based:
/// which slot is on air (per <see cref="IProgramGuideService"/>), where playback should be and
/// whether the ffmpeg process is still tracking that — with no persisted stream state, every
/// tick recomputes the truth from the clock, so a server restart resynchronizes by itself.
/// </summary>
public sealed class LiveStreamCoordinator
{
    /// <summary>The tolerated lag of the running process behind the program position before it
    /// is restarted at the correct offset.</summary>
    public const double MaxDriftSeconds = 30;

    private readonly IProgramGuideService _guide;
    private readonly LiveStreamMediaResolver _mediaResolver;
    private readonly LiveStreamSegmentStore _segmentStore;
    private readonly ILiveStreamSegmenter _segmenter;
    private readonly IFfmpegLocator _ffmpegLocator;
    private readonly ProgramSettingsService _settings;
    private readonly ILogger<LiveStreamCoordinator> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public LiveStreamCoordinator(
        IProgramGuideService guide,
        LiveStreamMediaResolver mediaResolver,
        LiveStreamSegmentStore segmentStore,
        ILiveStreamSegmenter segmenter,
        IFfmpegLocator ffmpegLocator,
        ProgramSettingsService settings,
        ILogger<LiveStreamCoordinator> logger)
    {
        _guide = guide;
        _mediaResolver = mediaResolver;
        _segmentStore = segmentStore;
        _segmenter = segmenter;
        _ffmpegLocator = ffmpegLocator;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Synchronizes the segmentation process with the schedule at <paramref name="utcNow"/> and
    /// runs the retention sweep.
    /// </summary>
    /// <param name="utcNow">The point in time to synchronize against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decision and the next due point in time.</returns>
    public async Task<LiveStreamSyncResult> SynchronizeAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var result = await SynchronizeStreamAsync(utcNow, cancellationToken);
        var retention = await _settings.GetLiveStreamSegmentRetentionAsync(cancellationToken);
        await _segmentStore.SweepExpiredSegmentsAsync(utcNow, retention, cancellationToken);
        return result;
    }

    private async Task<LiveStreamSyncResult> SynchronizeStreamAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        var ffmpegPath = _ffmpegLocator.Resolve();
        if (ffmpegPath is null)
        {
            await _segmenter.StopAsync(cancellationToken);
            return new LiveStreamSyncResult(LiveStreamSyncAction.FfmpegUnavailable, null,
                "ffmpeg wurde nicht gefunden (konfigurierter Pfad, tools/ffmpeg/, PATH).");
        }

        var guide = await _guide.GetNowAsync(utcNow, cancellationToken);
        var now = guide.Now;
        if (now is null)
        {
            await _segmenter.StopAsync(cancellationToken);
            return new LiveStreamSyncResult(LiveStreamSyncAction.Stopped, guide.Next?.StartUtc,
                "Sendelücke — keine laufende Sendung.");
        }

        // The segmenter's drain task can replace LastUnexpectedExit between two reads —
        // evaluate all classifications below against one consistent exit diagnosis.
        var lastExit = _segmenter.LastUnexpectedExit;

        // An input that already failed to demux on this slot fails on every input kind
        // and every retry — skip the rest of the slot instead of restart-looping. The
        // one-time diagnosis (stderr tail + pump statistics) sits in the segmenter's
        // ReportProcessExit warning, so this branch only reports via its Detail.
        if (lastExit is { SlotId: var undecodableSlot, UndecodableInput: true }
            && undecodableSlot == now.SlotId)
        {
            await _segmenter.StopAsync(cancellationToken);
            return new LiveStreamSyncResult(LiveStreamSyncAction.InputUnplayable, now.EndUtc,
                $"Sendeplatz {now.SlotId} ({now.Title}): Eingang für ffmpeg nicht lesbar — Sendeplatz wird bis zum Slotende übersprungen.");
        }

        // Input stages that already failed on this slot stay dropped for its remainder.
        // The per-slot failure flags accumulate on the exit report (this coordinator is
        // resolved per tick in a fresh scope — only the singleton segmenter can carry the
        // memory across ticks): accumulating, not just the last exit, is what lets the
        // cascade HTTP → sftp → pipe:0 reach the piped stream when the seekable stages
        // keep failing alternately. The stream token must outlive the slot's remaining
        // air time.
        var slotExit = lastExit?.SlotId == now.SlotId ? lastExit : null;
        var useHttpInput = !(slotExit is { HttpTransportFailure: true } or { HttpInputFailedForSlot: true });
        var useSftpInput = _segmenter.SftpInputSupported
            && !(slotExit is { SftpInputFailure: true } or { SftpInputFailedForSlot: true });
        var runDuration = now.RemainingSeconds is { } remaining
            ? TimeSpan.FromSeconds(remaining)
            : (TimeSpan?)null;
        var input = await _mediaResolver.ResolveAsync(now.MediaItemId, runDuration,
            useHttpInput, useSftpInput, cancellationToken: cancellationToken);
        if (input is null)
        {
            await _segmenter.StopAsync(cancellationToken);
            return new LiveStreamSyncResult(LiveStreamSyncAction.MediaUnavailable, now.EndUtc,
                $"Medium {now.MediaItemId} des laufenden Sendeplatzes nicht auflösbar.");
        }

        // The process state is not persisted: a crashed or drifted run restarts at the
        // slot's current program position (never at the position it died at) — the ~30 s
        // jumps of N15 are exactly this resync after up to one poll interval, not a
        // duplicate booking; only one process ever exists (_gate in the segmenter).
        if (_segmenter.IsRunning && _segmenter.CurrentSlotId == now.SlotId)
        {
            var expected = _segmenter.ExpectedPositionSeconds;
            var position = now.PositionSeconds ?? 0;
            if (expected is null || Math.Abs(expected.Value - position) <= MaxDriftSeconds)
                return new LiveStreamSyncResult(LiveStreamSyncAction.Unchanged, now.EndUtc);

            _logger.LogInformation(
                "[LiveStreamCoordinator] Stream für Sendeplatz {SlotId} driftet ab (erwartet {Expected:F1}s, Programm {Position:F1}s) — Neustart.",
                now.SlotId, expected ?? 0, position);
        }

        var segmentSeconds = (int)(await _settings.GetLiveStreamSegmentLengthAsync(cancellationToken)).TotalSeconds;
        var directory = await _segmentStore.GetDirectoryAsync(cancellationToken);
        var job = new LiveStreamJob(
            now.SlotId,
            input,
            now.PositionSeconds ?? 0,
            now.RemainingSeconds ?? 1,
            segmentSeconds,
            directory,
            ffmpegPath);

        await _segmenter.StartAsync(job, cancellationToken);
        var detail = $"Sendeplatz {now.SlotId} ({now.Title}) bei {job.PositionSeconds:F1}s — Eingang: {input.InputKindLabel}";
        if (input.ResolutionNote is { } note)
            detail += $" ({note})";
        return new LiveStreamSyncResult(LiveStreamSyncAction.Started, now.EndUtc, detail);
    }
}
