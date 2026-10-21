using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Data;
using VideoWebPlayer.Utils;

namespace VideoWebPlayer.Services;

/// <summary>
/// Probes pending video <see cref="MediaItem"/>s and persists their playback duration.
/// </summary>
public sealed class MediaDurationProbeService
{
    private readonly ApplicationDbContext _db;
    private readonly IMediaSourceReader _reader;
    private readonly IMediaDurationProbe _probe;
    private readonly IFfprobeLocator _locator;
    private readonly ILogger<MediaDurationProbeService> _logger;
    private readonly TimeProvider _timeProvider;

    // Static so the once-per-process warning survives the scoped service instances.
    private static bool _missingBinaryLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaDurationProbeService"/> class.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="reader">Media source reader used to open remote files.</param>
    /// <param name="probe">ffprobe-based duration probe.</param>
    /// <param name="locator">ffprobe executable locator.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public MediaDurationProbeService(
        ApplicationDbContext db,
        IMediaSourceReader reader,
        IMediaDurationProbe probe,
        IFfprobeLocator locator,
        ILogger<MediaDurationProbeService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _reader = reader;
        _probe = probe;
        _locator = locator;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Probes up to <paramref name="batchSize"/> pending video media items and stores the results.
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

        var items = await _db.MediaItems
            .Include(mi => mi.MediaCollection)
            .ThenInclude(mc => mc.MediaSource)
            .Where(mi => mi.DurationProbedAt == null)
            .Where(VideoExtensionPredicate())
            .OrderBy(mi => mi.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var probedAt = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await ProbeItemAsync(item, probedAt, cancellationToken);
        }

        return items.Count;
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

    private async Task ProbeItemAsync(MediaItem item, DateTime probedAt, CancellationToken cancellationToken)
    {
        try
        {
            TimeSpan? duration;
            if (item.MediaCollection.MediaSource.SourceType == MediaSourceType.LocalDirectory)
            {
                duration = await _probe.ProbeFileAsync(item.Path, cancellationToken);
            }
            else
            {
                await using var stream = _reader.OpenFileStream(item.MediaCollection, item.Name);
                duration = stream is null
                    ? null
                    : await _probe.ProbeStreamAsync(stream, cancellationToken);
            }

            item.Duration = duration;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Laufzeitermittlung für '{Path}' fehlgeschlagen.", item.Path);
            item.Duration = null;
        }

        item.DurationProbedAt = probedAt;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Laufzeit für '{Path}': {Duration}", item.Path, item.Duration?.ToString() ?? "nicht bestimmbar");
    }
}
