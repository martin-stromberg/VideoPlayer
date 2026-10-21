using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Utils;

namespace VideoWebPlayer.Services;

/// <summary>
/// Probes pending video <see cref="MediaItem"/>s and persists their playback duration. Failed
/// attempts are retried after a configurable delay until the configured attempt limit is reached.
/// </summary>
public sealed class MediaDurationProbeService
{
    private readonly ApplicationDbContext _db;
    private readonly IMediaSourceReader _reader;
    private readonly IMediaDurationProbe _probe;
    private readonly IFfprobeLocator _locator;
    private readonly MediaProbeOptions _options;
    private readonly ILogger<MediaDurationProbeService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IProgramScheduleSignal? _scheduleSignal;

    // Static so the once-per-process warning survives the scoped service instances.
    private static bool _missingBinaryLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaDurationProbeService"/> class.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="reader">Media source reader used to open remote files.</param>
    /// <param name="probe">ffprobe-based duration probe.</param>
    /// <param name="locator">ffprobe executable locator.</param>
    /// <param name="options">Media probe options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    /// <param name="scheduleSignal">Optional signal that wakes the program schedule worker once
    /// newly probed durations make media items schedulable.</param>
    public MediaDurationProbeService(
        ApplicationDbContext db,
        IMediaSourceReader reader,
        IMediaDurationProbe probe,
        IFfprobeLocator locator,
        IOptions<MediaProbeOptions> options,
        ILogger<MediaDurationProbeService> logger,
        TimeProvider? timeProvider = null,
        IProgramScheduleSignal? scheduleSignal = null)
    {
        _db = db;
        _reader = reader;
        _probe = probe;
        _locator = locator;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _scheduleSignal = scheduleSignal;
    }

    /// <summary>
    /// Probes up to <paramref name="batchSize"/> pending video media items and stores the results.
    /// Items whose last attempt failed transiently are eligible again once the configured retry
    /// delay has elapsed; items that exhausted the attempt limit are skipped.
    /// </summary>
    /// <param name="batchSize">Maximum number of items to probe in this call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of items processed; <c>0</c> when nothing is pending or ffprobe is unavailable.</returns>
    public async Task<int> ProbePendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
            return 0;

        if (_locator.Resolve() is null)
        {
            if (!_missingBinaryLogged)
            {
                _missingBinaryLogged = true;
                _logger.LogWarning(
                    "ffprobe wurde nicht gefunden. Die Laufzeitermittlung wartet, bis ein ffprobe-Binary verfügbar ist (MediaProbe:FfprobePath, tools/ffmpeg/ oder PATH).");
            }

            return 0;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var retryThreshold = now.AddSeconds(-_options.EffectiveRetryDelaySeconds);
        var maxAttempts = _options.EffectiveMaxAttempts;

        var items = await _db.MediaItems
            .Include(mi => mi.MediaCollection)
            .ThenInclude(mc => mc.MediaSource)
            .Where(mi => mi.Duration == null)
            .Where(mi => mi.DurationProbedAt == null
                || (mi.DurationProbeAttempts < maxAttempts && mi.DurationProbedAt <= retryThreshold))
            .Where(VideoExtensionPredicate())
            .OrderBy(mi => mi.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var outcomes = await RunProbesAsync(items, cancellationToken);

        var probedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var durationsWritten = false;
        foreach (var (item, outcome) in outcomes)
        {
            ApplyOutcome(item, outcome, probedAt, maxAttempts);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Laufzeit für '{Path}': {Duration} (Versuch {Attempts})",
                item.Path, item.Duration?.ToString() ?? "nicht bestimmbar", item.DurationProbeAttempts);

            durationsWritten |= item.Duration is not null;
        }

        if (durationsWritten)
        {
            _scheduleSignal?.NotifyScheduleChanged();
        }

        return items.Count;
    }

    /// <summary>
    /// Runs the actual ffprobe invocations for the given items, bounded by the configured probe
    /// parallelism. No database access happens on the probe tasks — the DbContext stays
    /// single-threaded; results are applied by the caller afterwards.
    /// </summary>
    private async Task<(MediaItem Item, ProbeOutcome Outcome)[]> RunProbesAsync(
        IReadOnlyList<MediaItem> items,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(Math.Max(1, _options.EffectiveParallelProbes));
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return (item, await ProbeItemAsync(item, cancellationToken));
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// The probe outcome of a single item, mapped to entity changes by <see cref="ApplyOutcome"/>.
    /// </summary>
    private enum ProbeOutcomeKind
    {
        Success,
        TransientFailure,
        PermanentFailure
    }

    private sealed record ProbeOutcome(ProbeOutcomeKind Kind, TimeSpan? Duration);

    private async Task<ProbeOutcome> ProbeItemAsync(MediaItem item, CancellationToken cancellationToken)
    {
        try
        {
            MediaDurationProbeResult result;
            if (item.MediaCollection.MediaSource.SourceType == MediaSourceType.LocalDirectory)
            {
                result = await _probe.ProbeFileAsync(item.Path, cancellationToken);
            }
            else
            {
                await using var stream = _reader.OpenFileStream(item.MediaCollection, item.Name);
                result = stream is null
                    ? MediaDurationProbeResult.TransientFailure
                    : await _probe.ProbeStreamAsync(stream, cancellationToken);
            }

            if (result.Duration is not null)
                return new ProbeOutcome(ProbeOutcomeKind.Success, result.Duration);

            return result.Transient
                ? new ProbeOutcome(ProbeOutcomeKind.TransientFailure, null)
                : new ProbeOutcome(ProbeOutcomeKind.PermanentFailure, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Laufzeitermittlung für '{Path}' fehlgeschlagen.", item.Path);
            return new ProbeOutcome(ProbeOutcomeKind.TransientFailure, null);
        }
    }

    private void ApplyOutcome(MediaItem item, ProbeOutcome outcome, DateTime probedAt, int maxAttempts)
    {
        switch (outcome.Kind)
        {
            case ProbeOutcomeKind.Success:
                item.Duration = outcome.Duration;
                item.DurationProbeAttempts = 0;
                break;
            case ProbeOutcomeKind.TransientFailure:
                item.Duration = null;
                item.DurationProbeAttempts++;
                break;
            case ProbeOutcomeKind.PermanentFailure:
                item.Duration = null;
                // Exhaust the attempt budget so the item is excluded from further retries.
                item.DurationProbeAttempts = Math.Max(item.DurationProbeAttempts + 1, maxAttempts);
                break;
        }

        item.DurationProbedAt = probedAt;
    }

    private static System.Linq.Expressions.Expression<Func<MediaItem, bool>> VideoExtensionPredicate()
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(MediaItem), "mi");
        var nameAccess = System.Linq.Expressions.Expression.Property(parameter, nameof(MediaItem.Name));

        System.Linq.Expressions.Expression? body = null;
        foreach (var extension in VideoFileExtensions.All)
        {
            var condition = System.Linq.Expressions.Expression.Call(
                nameAccess,
                nameof(string.EndsWith),
                Type.EmptyTypes,
                System.Linq.Expressions.Expression.Constant(extension));
            body = body is null ? condition : System.Linq.Expressions.Expression.OrElse(body, condition);
        }

        return System.Linq.Expressions.Expression.Lambda<Func<MediaItem, bool>>(body!, parameter);
    }
}
