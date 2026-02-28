using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceScanner"/> class.
        /// </summary>
        /// <param name="db">Application database context.</param>
        /// <param name="sftpReader">SFTP reader for remote sources.</param>
        /// <param name="logger">Logger instance.</param>
        public MediaSourceScanner(
            ApplicationDbContext db,
            SftpMediaSourceReader sftpReader,
            ILogger<MediaSourceScanner> logger,
            TimeProvider? timeProvider = null)
        {
            _db = db;
            _sftpReader = sftpReader;
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
            var next = await _db.MediaCollections
                .Include(mc => mc.MediaSource)
                .Where(mc => mc.ScanDueAt != null && mc.ScanDueAt <= now)
                .OrderBy(mc => mc.ScanDueAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is null)
                return false;

            _logger.LogInformation("Scanne Collection '{Path}'.", next.Path);

            foreach (var entry in _sftpReader.ReadDirectoryEntries(next))
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
            next.ScanDueAt = nowUtc.AddHours(24 * 7);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scan für Collection '{Path}' abgeschlossen. LastScannedAt={LastScannedAt}", next.Path, next.LastScannedAt);

            var childCollections = await _db.MediaCollections
                .Where(c => c.ParentMediaCollectionId == next.Id)
                .ToListAsync(cancellationToken);

            next.Classifyable = childCollections.Count == 0 || childCollections.All(c => c.Classifyable);
            await _db.SaveChangesAsync(cancellationToken);

            await UpdateParentClassifyableAsync(next.ParentMediaCollectionId, cancellationToken);

            return true;
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