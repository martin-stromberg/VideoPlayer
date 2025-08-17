using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services
{
    public class MediaSourceScanner
    {
        private readonly ApplicationDbContext _db;
        private readonly SftpMediaSourceReader _sftpReader;
        private readonly ILogger<MediaSourceScanner> _logger;

        public MediaSourceScanner(ApplicationDbContext db, SftpMediaSourceReader sftpReader, ILogger<MediaSourceScanner> logger)
        {
            _db = db;
            _sftpReader = sftpReader;
            _logger = logger;
        }

        public async Task ScanAllSourcesAsync(CancellationToken cancellationToken)
        {
            var sources = await _db.MediaSources.ToListAsync(cancellationToken);
            _logger.LogInformation("Starte Scan für {Count} Quellen.", sources.Count);

            foreach (var source in sources)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var lastScan = source.LastScannedAt ?? DateTime.MinValue;
                if ((DateTime.UtcNow - lastScan).TotalHours < 24)
                {
                    _logger.LogInformation("Quelle '{SourceName}' wurde vor weniger als 24h gescannt. Überspringe.", source.Name);
                    continue;
                }

                _logger.LogInformation("Scanne Quelle '{SourceName}'...", source.Name);
                await ScanSourceAsync(source, cancellationToken);

                source.LastScannedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Scan für Quelle '{SourceName}' abgeschlossen.", source.Name);
            }
        }

        private async Task ScanSourceAsync(MediaSource source, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Lese Root-Verzeichnis von Quelle '{SourceName}'...", source.Name);
            var counter = 0;
            foreach (var entry in _sftpReader.ReadRootDirectory(source))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    if (entry is MediaCollection dir)
                    {
                        var collection = await _db.EnsureMediaCollectionExistsAsync(dir, cancellationToken);
                        entry.Id = collection.Id;
                        await _db.SaveChangesAsync(cancellationToken);
                        _logger.LogDebug("Verzeichnis '{Path}' gescannt.", dir.Path);
                        counter = 0;
                    }
                    else if (entry is MediaItem file)
                    {
                        var item = await _db.EnsureMediaItemExistsAsync(file, cancellationToken);
                        entry.Id = item.Id;
                        await _db.SaveChangesAsync(cancellationToken);
                        if (counter++ % 100 == 0)
                            _logger.LogDebug("Datei '{Path}' gescannt.", file.Path);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Verarbeiten von '{EntryName}' in Quelle '{SourceName}'.", entry.Name, source.Name);
                }
            }
        }
    }
}