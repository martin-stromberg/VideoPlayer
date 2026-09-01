using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Renci.SshNet.Common;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Scans media sources for new or updated items.
    /// </summary>
    public class MediaSourceScanner
    {
        private readonly ApplicationDbContext _db;
        private readonly SftpMediaSourceReader _sftpReader;
        private readonly ILogger<MediaSourceScanner> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly ProgramSettingsService _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceScanner"/> class.
        /// </summary>
        /// <param name="db">Application database context.</param>
        /// <param name="sftpReader">SFTP reader for remote sources.</param>
        /// <param name="logger">Logger instance.</param>
        public MediaSourceScanner(
            ApplicationDbContext db,
            SftpMediaSourceReader sftpReader,
            ProgramSettingsService settings,
            ILogger<MediaSourceScanner> logger,
            TimeProvider? timeProvider = null)
        {
            _db = db;
            _sftpReader = sftpReader;
            _settings = settings;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Scans all configured media sources and updates their state.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task ScanAllSourcesAsync(CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            // Ensure DateTime kind is UTC so EF/core and tests don't mix kinds/local conversions
            var nowUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc);
            var sources = await _db.MediaSources.ToListAsync(cancellationToken);
            _logger.LogInformation("Starte Root-Scan für {Count} Quellen.", sources.Count);

            foreach (var source in sources)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                // mark the source as scanned at this time so downstream file providers
                // and tests can validate the timing
                source.LastScannedAt = nowUtc;
                await _db.SaveChangesAsync(cancellationToken);

                var rootEntry = _sftpReader.ReadRootDirectory(source).OfType<MediaCollection>().FirstOrDefault();
                if (rootEntry is null)
                    continue;

                var existing = await _db.MediaCollections.FirstOrDefaultAsync(
                    c => c.MediaSourceId == source.Id && c.Path == rootEntry.Path,
                    cancellationToken);

                if (existing is null)
                {
                    rootEntry.MediaSource = source;
                    rootEntry.MediaSourceId = source.Id;
                    rootEntry.LastScannedAt = null;
                    rootEntry.ClassifiedAt = null;
                    rootEntry.Classifyable = false;
                    rootEntry.ScanDueAt = nowUtc;
                    _db.MediaCollections.Add(rootEntry);
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Root-Collection '{Path}' angelegt.", rootEntry.Path);
                }
                else if (existing.CreatedAt != rootEntry.CreatedAt)
                {
                    // Update the existing DB entity (not the newly read rootEntry) so changes are persisted
                    existing.Classifyable = false;
                    existing.ScanDueAt = nowUtc;
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Root-Collection '{Path}' aktualisiert.", existing.Path);
                }
            }
        }

        /// <summary>
        /// Scans the next media collection incrementally and updates its scan state.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task<bool> ScanNextMediaCollection(CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var nowUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc);

            var (_, mediaCollectionInterval) = await _settings.GetScanIntervalsAsync(cancellationToken);
            var next = await _db.MediaCollections
                .Include(mc => mc.MediaSource)
                .Where(mc => mc.ScanDueAt != null && mc.ScanDueAt <= nowUtc)
                .OrderBy(mc => mc.ScanDueAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is null)
                return false;

            await ScanMediaCollectionInternalAsync(next, nowUtc, mediaCollectionInterval, cancellationToken);
            return true;
        }

        /// <summary>
        /// Scans a specific media collection by identifier.
        /// </summary>
        /// <param name="mediaCollectionId">The media collection identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task<bool> ScanMediaCollectionAsync(long mediaCollectionId, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var nowUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc);

            var (_, mediaCollectionInterval) = await _settings.GetScanIntervalsAsync(cancellationToken);

            var collection = await _db.MediaCollections
                .Include(mc => mc.MediaSource)
                .FirstOrDefaultAsync(mc => mc.Id == mediaCollectionId, cancellationToken);

            if (collection is null)
                return false;

            await ScanMediaCollectionInternalAsync(collection, nowUtc, mediaCollectionInterval, cancellationToken);
            return true;
        }

        /// <summary>
        /// Performs a complete scan of the given collection and all its (existing or newly discovered) child collections.
        /// </summary>
        /// <param name="rootMediaCollectionId">Root media collection identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The number of scanned collections.</returns>
        public async Task<int> ScanCollectionTreeAsync(long rootMediaCollectionId, CancellationToken cancellationToken)
        {
            var root = await _db.MediaCollections.FirstOrDefaultAsync(c => c.Id == rootMediaCollectionId, cancellationToken);
            if (root is null)
                return 0;

            var (_, mediaCollectionInterval) = await _settings.GetScanIntervalsAsync(cancellationToken);

            root.ScanDueAt = DateTime.MinValue;
            root.Classifyable = false;
            await _db.SaveChangesAsync(cancellationToken);

            var visited = new HashSet<long>();
            var queue = new Queue<long>();
            queue.Enqueue(rootMediaCollectionId);

            var scannedCount = 0;

            while (queue.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var id = queue.Dequeue();
                if (!visited.Add(id))
                    continue;

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var nowUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc);

                var current = await _db.MediaCollections
                    .Include(mc => mc.MediaSource)
                    .FirstOrDefaultAsync(mc => mc.Id == id, cancellationToken);

                if (current is null)
                    continue;

                await ScanMediaCollectionInternalAsync(current, nowUtc, mediaCollectionInterval, cancellationToken);
                scannedCount++;

                var childIds = await _db.MediaCollections
                    .Where(c => c.ParentMediaCollectionId == id)
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);

                foreach (var childId in childIds)
                    queue.Enqueue(childId);
            }

            return scannedCount;
        }

        private async Task ScanMediaCollectionInternalAsync(MediaCollection next, DateTime nowUtc, TimeSpan mediaCollectionScanInterval, CancellationToken cancellationToken)
        {

            _logger.LogInformation("Scanne Collection '{Path}'.", next.Path);

            List<MediaEntry> entries;
            try
            {
                entries = _sftpReader.ReadDirectoryEntries(next).ToList();
            }
            catch (Exception ex) when (ex is SftpPathNotFoundException or SftpPermissionDeniedException)
            {
                _logger.LogWarning(ex, "Collection '{Path}' konnte nicht gelesen werden und wird übersprungen.", next.Path);

                next.LastScannedAt = nowUtc;
                next.ScanDueAt = nowUtc.Add(mediaCollectionScanInterval);
                next.Classifyable = true;
                await _db.SaveChangesAsync(cancellationToken);

                await UpdateParentClassifyableAsync(next.ParentMediaCollectionId, cancellationToken);
                return;
            }

            foreach (var entry in entries)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (entry is MediaCollection folder)
                {
                    var exists = await _db.MediaCollections.FirstOrDefaultAsync(
                        c => c.MediaSourceId == next.MediaSourceId && c.Path == folder.Path,
                        cancellationToken);
                    if (exists is null)
                    {
                        folder.LastScannedAt = null;
                        folder.ClassifiedAt = null;
                        folder.Classifyable = false;
                        folder.ScanDueAt = nowUtc;
                        _db.MediaCollections.Add(folder);
                        _logger.LogInformation("Neue MediaCollection angelegt: {Path}", folder.Path);
                    }
                    else if (exists.CreatedAt != folder.CreatedAt)
                    {
                        // remote folder changed -> schedule rescan
                        exists.Classifyable = false;
                        exists.ScanDueAt = nowUtc;
                        await _db.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Vorhandene MediaCollection aktualisiert (CreatedAt geändert): {Path}", exists.Path);
                    }
                }
                else if (entry is MediaItem file)
                {
                    var existingItem = await _db.MediaItems.FirstOrDefaultAsync(
                        i => i.MediaCollectionId == next.Id && i.Path == file.Path,
                        cancellationToken);
                    if (existingItem is null)
                    {
                        _db.MediaItems.Add(file);
                        _logger.LogInformation("Neues MediaItem angelegt: {Path}", file.Path);
                    }
                    else if (existingItem.CreatedAt != file.CreatedAt)
                    {
                        // remote file changed -> update timestamps and mark changed for classifier
                        existingItem.CreatedAt = file.CreatedAt;
                        existingItem.Changed = true;
                        existingItem.ClassifiedAt = null;
                        await _db.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Vorhandenes MediaItem aktualisiert (CreatedAt geändert): {Path}", existingItem.Path);
                    }
                }
            }
            next.LastScannedAt = nowUtc;
            next.ScanDueAt = nowUtc.Add(mediaCollectionScanInterval);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scan für Collection '{Path}' abgeschlossen. LastScannedAt={LastScannedAt}", next.Path, next.LastScannedAt);

            var childCollections = await _db.MediaCollections
                .Where(c => c.ParentMediaCollectionId == next.Id)
                .ToListAsync(cancellationToken);

            next.Classifyable = childCollections.Count == 0 || childCollections.All(c => c.Classifyable);
            await _db.SaveChangesAsync(cancellationToken);

            await UpdateParentClassifyableAsync(next.ParentMediaCollectionId, cancellationToken);
        }

        private async Task UpdateParentClassifyableAsync(long? parentId, CancellationToken cancellationToken)
        {
            while (parentId.HasValue)
            {
                var parent = await _db.MediaCollections.FirstOrDefaultAsync(
                    c => c.Id == parentId.Value,
                    cancellationToken);
                if (parent is null)
                    return;

                var allChildrenClassifyable = await _db.MediaCollections
                    .Where(c => c.ParentMediaCollectionId == parent.Id)
                    .AllAsync(c => c.Classifyable, cancellationToken);

                var newValue = allChildrenClassifyable;
                parent.Classifyable = newValue;
                await _db.SaveChangesAsync(cancellationToken);

                if (!newValue)
                    break;

                parentId = parent.ParentMediaCollectionId;
            }
        }
    }
}