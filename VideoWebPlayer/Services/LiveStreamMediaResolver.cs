using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Resolves a broadcast slot's <see cref="ProgramSlot.MediaItemId"/> to the ffmpeg input the
/// segmenter should run on: a physical file path for local-directory sources (fast seeking,
/// robust for every container layout) or — for remote sources — preferrably the URL of the
/// server's own range-capable media-stream endpoint, with the streamed stdin input as
/// fallback when HTTP input is disabled or no URI can be built.
/// </summary>
public sealed class LiveStreamMediaResolver
{
    /// <summary>Buffer added to the slot's remaining air time when sizing the stream token's
    /// lifetime — covers coordinator tick latency and restarts within the same slot.</summary>
    private static readonly TimeSpan StreamTokenLifetimeMargin = TimeSpan.FromMinutes(15);

    /// <summary>Default token lifetime when no remaining run duration is known.</summary>
    private static readonly TimeSpan DefaultStreamTokenLifetime = TimeSpan.FromMinutes(45);

    private readonly ApplicationDbContext _db;
    private readonly IMediaSourceReader _reader;
    private readonly LocalMediaSourceReader _localReader;
    private readonly IInternalMediaStreamTokenService _streamTokens;
    private readonly LiveStreamOptions _liveStreamOptions;
    private readonly ILogger<LiveStreamMediaResolver> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="reader">Media source reader dispatcher.</param>
    /// <param name="localReader">Local directory reader for physical path resolution.</param>
    /// <param name="streamTokens">Internal stream token service for the HTTP input URI.</param>
    /// <param name="liveStreamOptions">Live stream options.</param>
    /// <param name="logger">Logger instance.</param>
    public LiveStreamMediaResolver(
        ApplicationDbContext db,
        IMediaSourceReader reader,
        LocalMediaSourceReader localReader,
        IInternalMediaStreamTokenService streamTokens,
        IOptions<LiveStreamOptions> liveStreamOptions,
        ILogger<LiveStreamMediaResolver> logger)
    {
        _db = db;
        _reader = reader;
        _localReader = localReader;
        _streamTokens = streamTokens;
        _liveStreamOptions = liveStreamOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the media item to a segmentation input, or <see langword="null"/> when the item,
    /// its collection or the file itself is unavailable — the slot then behaves like a schedule
    /// gap for the stream.
    /// </summary>
    /// <param name="mediaItemId">The slot's media item identifier.</param>
    /// <param name="runDuration">
    /// How long the resulting input is expected to run (the slot's remaining air time) —
    /// sizes the HTTP stream token's lifetime; <see langword="null"/> uses a default.
    /// </param>
    /// <param name="useHttpInput">
    /// Whether the range-capable HTTP input may be used for remote sources. Callers pass
    /// <see langword="false"/> after an HTTP transport failure so the next attempt uses the
    /// piped stream again.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved input, or <see langword="null"/>.</returns>
    public async Task<LiveStreamInput?> ResolveAsync(
        long mediaItemId,
        TimeSpan? runDuration = null,
        bool useHttpInput = true,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.MediaItems
            .AsNoTracking()
            .FirstOrDefaultAsync(mi => mi.Id == mediaItemId, cancellationToken);
        if (item is null)
        {
            _logger.LogWarning("[LiveStreamMediaResolver] MediaItem {MediaItemId} nicht gefunden.", mediaItemId);
            return null;
        }

        var collection = await _db.MediaCollections
            .Include(mc => mc.MediaSource)
            .AsNoTracking()
            .FirstOrDefaultAsync(mc => mc.Id == item.MediaCollectionId, cancellationToken);
        if (collection?.MediaSource is null)
        {
            _logger.LogWarning("[LiveStreamMediaResolver] Collection {CollectionId} fuer MediaItem {MediaItemId} nicht gefunden.", item.MediaCollectionId, mediaItemId);
            return null;
        }

        var fileName = Path.GetFileName(item.Path);
        if (string.IsNullOrEmpty(fileName))
        {
            _logger.LogWarning("[LiveStreamMediaResolver] MediaItem {MediaItemId} ohne Dateinamen.", mediaItemId);
            return null;
        }

        if (collection.MediaSource.SourceType == MediaSourceType.LocalDirectory)
        {
            var path = _localReader.ResolveLocalFilePath(collection, fileName);
            if (path is null)
            {
                _logger.LogWarning("[LiveStreamMediaResolver] Datei '{FileName}' in Collection {CollectionId} nicht aufloesbar.", fileName, collection.Id);
                return null;
            }

            return LiveStreamInput.ForFile(path);
        }

        if (useHttpInput && _liveStreamOptions.UseHttpInput)
        {
            var lifetime = runDuration is { } duration
                ? duration + StreamTokenLifetimeMargin
                : DefaultStreamTokenLifetime;
            var uri = _streamTokens.TryCreateStreamUri(item.Id, lifetime);
            if (uri is not null)
                return LiveStreamInput.ForHttp(uri);
        }

        return LiveStreamInput.ForStream(_ => Task.FromResult(_reader.OpenFileStream(collection, fileName)));
    }
}
