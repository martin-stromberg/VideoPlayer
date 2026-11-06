using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Ergebnis eines Komplettscans über <see cref="MediaSourceScanCoordinator.RunFullScanAsync"/>.
    /// </summary>
    /// <param name="NewItemsCount">Anzahl der während des Laufs neu hinzugekommenen MediaItems.</param>
    /// <param name="CollectionsScanned">Anzahl der in der Collection-Schleife gescannten Collections.</param>
    /// <param name="CapReached">Ob die Obergrenze (<c>maxCollectionsPerRun</c>) den Lauf begrenzt hat.</param>
    /// <returns>Das Ergebnis des Komplettscans.</returns>
    public sealed record MediaSourceScanRunResult(int NewItemsCount, int CollectionsScanned, bool CapReached);

    /// <summary>
    /// Ergebnis eines Collection-Rescans über <see cref="MediaSourceScanCoordinator.RescanCollectionTreeAsync"/>.
    /// </summary>
    /// <param name="NewItemsCount">Anzahl der während des Laufs neu hinzugekommenen MediaItems.</param>
    /// <param name="ClassificationDeferred">Ob die Klassifizierung mangels freier Kapazität in die Warteschlange gestellt wurde.</param>
    /// <returns>Das Ergebnis des Collection-Rescans.</returns>
    public sealed record CollectionRescanResult(int NewItemsCount, bool ClassificationDeferred);

    /// <summary>
    /// Einstiegspunkt für die Scan-Pipeline einer Medienquelle: kapselt Wecksignal-Scopes, Gate-Lease,
    /// Scan, Benachrichtigung, Genre-Reload, Collection-Schleife und Klassifizierung in der
    /// Worker-Reihenfolge.
    /// </summary>
    public class MediaSourceScanCoordinator
    {
        private readonly MediaSourceScanner _scanner;
        private readonly MediaSourceClassifier _classifier;
        private readonly IPlaylistBackfillSignal _playlistBackfillSignal;
        private readonly IProgramScheduleSignal _programScheduleSignal;
        private readonly MediaUpdateNotificationService _notificationService;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<MediaSourceScanCoordinator> _logger;
        private readonly IBackgroundProcessingGate? _gate;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceScanCoordinator"/> class.
        /// </summary>
        /// <param name="scanner">Scanner für Medienquellen und Collections.</param>
        /// <param name="classifier">Classifier für MediaItems und MediaCollections.</param>
        /// <param name="playlistBackfillSignal">Wecksignal für den Playlist-Backfill-Worker.</param>
        /// <param name="programScheduleSignal">Wecksignal für den Sendeplan-Worker.</param>
        /// <param name="notificationService">Benachrichtigungsdienst für neue Videos.</param>
        /// <param name="db">Application-Datenbankkontext.</param>
        /// <param name="logger">Logger-Instanz.</param>
        /// <param name="gate">Optionales Gate zur Koordination mit Restore-Operationen.</param>
        public MediaSourceScanCoordinator(
            MediaSourceScanner scanner,
            MediaSourceClassifier classifier,
            IPlaylistBackfillSignal playlistBackfillSignal,
            IProgramScheduleSignal programScheduleSignal,
            MediaUpdateNotificationService notificationService,
            ApplicationDbContext db,
            ILogger<MediaSourceScanCoordinator> logger,
            IBackgroundProcessingGate? gate = null)
        {
            _scanner = scanner;
            _classifier = classifier;
            _playlistBackfillSignal = playlistBackfillSignal;
            _programScheduleSignal = programScheduleSignal;
            _notificationService = notificationService;
            _db = db;
            _logger = logger;
            _gate = gate;
        }

        /// <summary>
        /// Führt einen vollständigen Scan aller Medienquellen inklusive Klassifizierung durch.
        /// </summary>
        /// <param name="operationName">Name der Gate-Operation (Logs/Diagnose).</param>
        /// <param name="maxCollectionsPerRun">Obergrenze gescannter Collections je Lauf, oder <c>null</c> für ungedeckelt.</param>
        /// <param name="cancellationToken">Abbruchtoken.</param>
        /// <returns>Das Ergebnis des Laufs.</returns>
        public async Task<MediaSourceScanRunResult> RunFullScanAsync(string operationName, int? maxCollectionsPerRun, CancellationToken cancellationToken = default)
        {
            if (maxCollectionsPerRun.HasValue && maxCollectionsPerRun.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCollectionsPerRun));

            // Marker-Commits und Sendeplan-Änderungen während des Laufs wecken nicht; das Laufende weckt genau einmal.
            using var playlistBackfillScanScope = _playlistBackfillSignal.BeginScan();
            using var programScheduleScanScope = _programScheduleSignal.BeginScan();
            await using var processingLease = _gate is null ? null : await _gate.EnterOperationAsync(operationName, cancellationToken);

            _logger.LogInformation("Starte Scan aller Quellen.");

            int newItemsCount = await CountNewItemsAsync(ct => _scanner.ScanAllSourcesAsync(ct), cancellationToken);
            _logger.LogInformation("Scan aller Quellen abgeschlossen. {NewItems} neue Items gefunden.", newItemsCount);

            await _notificationService.NotifyNewVideosScannedAsync(0L, newItemsCount, cancellationToken);

            _logger.LogInformation("Prüfe, ob Genres neu geladen werden müssen.");
            await _classifier.CheckReloadGenres(cancellationToken);

            var scannedCollections = 0;

            while (!maxCollectionsPerRun.HasValue || scannedCollections < maxCollectionsPerRun.Value)
            {
                if (!await _scanner.ScanNextMediaCollection(cancellationToken))
                    break;

                scannedCollections++;
                _logger.LogInformation("Eine Collection wurde gescannt (ScanNextMediaCollection gab 'true' zurück).");
            }

            var capReached = maxCollectionsPerRun.HasValue && scannedCollections >= maxCollectionsPerRun.Value;

            if (scannedCollections > 0)
            {
                _logger.LogInformation("Klassifiziere MediaItems nach ScanNextMediaCollection.");
                await _classifier.ClassifyMediaItemsAsync(cancellationToken);
                _logger.LogInformation("Klassifizierung abgeschlossen.");

                await ClassifyPendingCollectionsAsync(requireUnclassified: true, cancellationToken);
            }
            else
            {
                await ClassifyPendingCollectionsAsync(requireUnclassified: false, cancellationToken);
            }

            return new MediaSourceScanRunResult(newItemsCount, scannedCollections, capReached);
        }

        /// <summary>
        /// Scannt und klassifiziert den Teilbaum einer Collection neu.
        /// </summary>
        /// <param name="collectionId">Id der Root-Collection des Teilbaums.</param>
        /// <param name="operationName">Name der Gate-Operation (Logs/Diagnose).</param>
        /// <param name="cancellationToken">Abbruchtoken.</param>
        /// <returns>Das Ergebnis des Laufs.</returns>
        public async Task<CollectionRescanResult> RescanCollectionTreeAsync(long collectionId, string operationName, CancellationToken cancellationToken = default)
        {
            var collectionExists = await _db.MediaCollections.AnyAsync(c => c.Id == collectionId, cancellationToken);
            if (!collectionExists)
                return new CollectionRescanResult(0, false);

            using var playlistBackfillScanScope = _playlistBackfillSignal.BeginScan();
            using var programScheduleScanScope = _programScheduleSignal.BeginScan();
            await using var processingLease = _gate is null ? null : await _gate.EnterOperationAsync(operationName, cancellationToken);

            int newItemsCount = await CountNewItemsAsync(ct => _scanner.ScanCollectionTreeAsync(collectionId, ct), cancellationToken);

            await _notificationService.NotifyNewVideosScannedAsync(0L, newItemsCount, cancellationToken);

            await _classifier.CheckReloadGenres(cancellationToken);

            var classified = await _classifier.ClassifyCollectionTreeAsync(collectionId, cancellationToken);

            return new CollectionRescanResult(newItemsCount, !classified);
        }

        private async Task<int> CountNewItemsAsync(Func<CancellationToken, Task> scanAction, CancellationToken cancellationToken)
        {
            int itemsBeforeScan = await _db.MediaItems.CountAsync(cancellationToken);

            await scanAction(cancellationToken);

            int itemsAfterScan = await _db.MediaItems.CountAsync(cancellationToken);

            return itemsAfterScan - itemsBeforeScan;
        }

        private async Task ClassifyPendingCollectionsAsync(bool requireUnclassified, CancellationToken cancellationToken)
        {
            int unclassifiedBefore = await _db.MediaCollections
                .CountAsync(mc => mc.Classifyable && (mc.Changed || !mc.ClassifiedAt.HasValue), cancellationToken);

            if (requireUnclassified && unclassifiedBefore == 0)
                return;

            _logger.LogInformation("Scan abgeschlossen. Starte Klassifizierung ({UnclassifiedCount} unclassifizierte Collections).", unclassifiedBefore);
            await _classifier.ClassifyMediaCollectionsAsync(cancellationToken);
            _logger.LogInformation("Klassifizierung abgeschlossen.");

            await _notificationService.NotifyNewVideosScannedAsync(0L, unclassifiedBefore, cancellationToken);
        }
    }
}
