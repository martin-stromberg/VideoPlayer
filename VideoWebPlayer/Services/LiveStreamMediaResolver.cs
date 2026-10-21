using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Resolves a broadcast slot's <see cref="ProgramSlot.MediaItemId"/> to the ffmpeg input the
/// segmenter should run on: a physical file path for local-directory sources (fast seeking,
/// robust for every container layout) or a streamed input for remote sources.
/// </summary>
public sealed class LiveStreamMediaResolver
{
    private readonly ApplicationDbContext _db;
    private readonly IMediaSourceReader _reader;
    private readonly LocalMediaSourceReader _localReader;
    private readonly ILogger<LiveStreamMediaResolver> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="reader">Media source reader dispatcher.</param>
    /// <param name="localReader">Local directory reader for physical path resolution.</param>
    /// <param name="logger">Logger instance.</param>
    public LiveStreamMediaResolver(
        ApplicationDbContext db,
        IMediaSourceReader reader,
        LocalMediaSourceReader localReader,
        ILogger<LiveStreamMediaResolver> logger)
    {
        _db = db;
        _reader = reader;
        _localReader = localReader;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the media item to a segmentation input, or <see langword="null"/> when the item,
    /// its collection or the file itself is unavailable — the slot then behaves like a schedule
    /// gap for the stream.
    /// </summary>
    /// <param name="mediaItemId">The slot's media item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved input, or <see langword="null"/>.</returns>
    public async Task<LiveStreamInput?> ResolveAsync(long mediaItemId, CancellationToken cancellationToken = default)
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

        return LiveStreamInput.ForStream(_ => Task.FromResult(_reader.OpenFileStream(collection, fileName)));
    }
}
