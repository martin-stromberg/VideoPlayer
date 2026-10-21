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
    private readonly IInternalMediaStreamTokenService? _streamTokens;

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
    /// <param name="streamTokens">Optional internal stream token service that builds the
    /// loopback HTTP probe URIs; when absent, remote items fall back directly to the
    /// <c>sftp://</c>/<c>pipe:0</c> paths.</param>
    public MediaDurationProbeService(
        ApplicationDbContext db,
        IMediaSourceReader reader,
        IMediaDurationProbe probe,
        IFfprobeLocator locator,
        IOptions<MediaProbeOptions> options,
        ILogger<MediaDurationProbeService> logger,
        TimeProvider? timeProvider = null,
        IProgramScheduleSignal? scheduleSignal = null,
        IInternalMediaStreamTokenService? streamTokens = null)
    {
        _db = db;
        _reader = reader;
        _probe = probe;
        _locator = locator;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _scheduleSignal = scheduleSignal;
        _streamTokens = streamTokens;
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
    /// Aggregates the duration-probe progress for the administration view: counters over all
    /// video media items (probed / pending or retrying / permanently failed), the timestamp of
    /// the most recent probe attempt, the number of attempts within the last hour and the most
    /// recently probed items. Administrator-only.
    /// </summary>
    /// <param name="userId">The requesting user; must be an administrator.</param>
    /// <param name="recentCount">How many recent probe results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated progress.</returns>
    public async Task<MediaDurationProbeProgress> GetProgressAsync(
        string userId, int recentCount = 10, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);

        var maxAttempts = _options.EffectiveMaxAttempts;
        var videos = _db.MediaItems.AsNoTracking().Where(VideoExtensionPredicate());

        var total = await videos.CountAsync(cancellationToken);
        var probed = await videos.CountAsync(mi => mi.Duration != null, cancellationToken);
        var failed = await videos.CountAsync(
            mi => mi.Duration == null && mi.DurationProbeAttempts >= maxAttempts, cancellationToken);

        var lastActivity = await videos.MaxAsync(mi => mi.DurationProbedAt, cancellationToken);
        var hourAgo = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-1);
        var probedLastHour = await videos.CountAsync(mi => mi.DurationProbedAt >= hourAgo, cancellationToken);

        List<MediaDurationProbeRecentItem> recent;
        if (recentCount <= 0)
        {
            recent = new List<MediaDurationProbeRecentItem>();
        }
        else
        {
            recent = await videos
                .Where(mi => mi.DurationProbedAt != null)
                .OrderByDescending(mi => mi.DurationProbedAt)
                .Take(recentCount)
                .Select(mi => new MediaDurationProbeRecentItem(
                    mi.Name,
                    mi.MediaCollection.MediaSource.Name,
                    mi.DurationProbedAt!.Value,
                    mi.Duration,
                    mi.DurationProbeAttempts))
                .ToListAsync(cancellationToken);
        }

        return new MediaDurationProbeProgress(
            total, probed, total - probed - failed, failed, lastActivity, probedLastHour, recent);
    }

    private async Task RequireAdminAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Sie sind nicht angemeldet.");

        if (!user.IsAdmin)
            throw new ForbiddenAccessException("Nur Administratoren dürfen die Laufzeitermittlung einsehen.");
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
                result = await ProbeRemoteItemAsync(item, cancellationToken);
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

    /// <summary>
    /// Probes an item of a remote source. The preferred path hands ffprobe a loopback
    /// <c>http://</c> URI of the internal media-stream endpoint — ffprobe seeks with HTTP
    /// range requests through the same media-source read path as playback (only container
    /// headers, seconds instead of the whole file), independently of the source type. When
    /// that URI cannot be built or the endpoint cannot serve it, the probe falls back to a
    /// seekable <c>sftp://</c> URI for SFTP sources with credentials (libssh reads only the
    /// container headers) and finally to the streamed <c>pipe:0</c> input — all within the
    /// same attempt.
    /// </summary>
    private async Task<MediaDurationProbeResult> ProbeRemoteItemAsync(MediaItem item, CancellationToken cancellationToken)
    {
        var collection = item.MediaCollection;
        var source = collection.MediaSource;

        var httpUri = _streamTokens?.TryCreateProbeUri(item.Id);
        if (httpUri is not null)
        {
            var httpResult = await _probe.ProbeUriAsync(httpUri, cancellationToken);
            if (!httpResult.UriUnsupported)
                return httpResult;
        }

        if (source.SourceType == MediaSourceType.Sftp)
        {
            var uri = SftpProbeUriBuilder.TryBuildUri(
                source.Host, source.Port, source.Username, source.Password, ResolveRemotePath(item));
            if (uri is not null)
            {
                var uriResult = await _probe.ProbeUriAsync(uri, cancellationToken);
                if (!uriResult.UriUnsupported)
                    return uriResult;
            }
            else
            {
                _logger.LogDebug(
                    "Für '{Path}' kann keine sftp://-Probe-URL gebildet werden (fehlende Credentials oder kein absoluter Remote-Pfad) — Fallback auf gestreamten Probe.",
                    item.Path);
            }
        }

        await using var stream = _reader.OpenFileStream(collection, item.Name);
        return stream is null
            ? MediaDurationProbeResult.TransientFailure
            : await _probe.ProbeStreamAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Resolves the absolute path of <paramref name="item"/> on its SFTP server:
    /// <see cref="MediaEntry.Path"/> already carries the full remote path recorded at scan time;
    /// otherwise collection path and file name are combined like
    /// <see cref="IMediaSourceReader.OpenFileStream"/> does. Returns <see langword="null"/>
    /// when no absolute path can be formed.
    /// </summary>
    private static string? ResolveRemotePath(MediaItem item)
    {
        var path = item.Path;
        if (!string.IsNullOrEmpty(path) && path[0] == '/')
            return path;

        var combined = SftpProbeUriBuilder.CombinePath(
            item.MediaCollection.Path, string.IsNullOrEmpty(path) ? item.Name : path);
        return combined.Length > 0 && combined[0] == '/' ? combined : null;
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
