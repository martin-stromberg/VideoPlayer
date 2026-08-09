using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Background service that scans media sources and runs classification tasks.
    /// </summary>
    public class MediaSourceScanService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly EventManager _eventManager;
        private readonly ILogger<MediaSourceScanService> _logger;
        private readonly MediaUpdateNotificationService _notificationService;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _loopDelay;
        private readonly bool _skipUpgrade;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceScanService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider used to create scopes.</param>
        /// <param name="eventManager">Event manager instance.</param>
        /// <param name="notificationService">Service for sending SignalR notifications.</param>
        /// <param name="logger">Logger instance.</param>
        public MediaSourceScanService(
            IServiceProvider serviceProvider,
            EventManager eventManager,
            MediaUpdateNotificationService notificationService,
            ILogger<MediaSourceScanService> logger,
            TimeSpan? initialDelay = null,
            TimeSpan? loopDelay = null,
            bool skipUpgrade = false,
            TimeProvider? timeProvider = null)
        {
            _serviceProvider = serviceProvider;
            _eventManager = eventManager;
            _notificationService = notificationService;
            _logger = logger;
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(10);
            _loopDelay = loopDelay ?? TimeSpan.FromMinutes(1);
            _skipUpgrade = skipUpgrade;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Executes the background scanning loop.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to stop processing.</param>
        /// <returns>A task that represents the background operation.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (!_skipUpgrade)
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var upgradeManager = scope.ServiceProvider.GetRequiredService<DataUpgradeManager>();
                        _logger.LogInformation("Starte Datenbank-Upgrade.");
                        await upgradeManager.EnsureUpToDateAsync(stoppingToken);
                        _logger.LogInformation("Datenbank-Upgrade abgeschlossen.");
                    }
                }
                if (_initialDelay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Warte {Delay}.", _initialDelay);
                    await Task.Delay(_initialDelay, stoppingToken);
                }
                var lastRun = DateTimeOffset.MinValue;
                using var settingsScope = _serviceProvider.CreateScope();
                
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var now = _timeProvider.GetUtcNow();
                        using var innerScope = _serviceProvider.CreateScope();
                        var scanner = innerScope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
                        var classifier = innerScope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();
                        var gate = innerScope.ServiceProvider.GetService<VideoWebPlayer.Services.Backups.IBackgroundProcessingGate>();
                        var settings = settingsScope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
                        var (scanProcessInterval, _) = await settings.GetScanIntervalsAsync(stoppingToken);

                        if (scanProcessInterval <= TimeSpan.Zero)
                            scanProcessInterval = TimeSpan.FromHours(1);

                        if (now - lastRun >= scanProcessInterval)
                        {
                            await using var processingLease = gate is null ? null : await gate.EnterOperationAsync("Automatischer Scanprozess", stoppingToken);
                            _logger.LogInformation("Starte Scanprozess (Intervall: {Interval}).", scanProcessInterval);

                            _logger.LogInformation("Starte Scan aller Quellen.");

                            // Z�hle Items vor dem Scan
                            int itemsBeforeScan;
                            {
                                var db = innerScope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                                itemsBeforeScan = await db.MediaItems.CountAsync(stoppingToken);
                            }

                            await scanner.ScanAllSourcesAsync(stoppingToken);

                            // Z�hle Items nach dem Scan
                            int itemsAfterScan;
                            {
                                var db = innerScope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                                itemsAfterScan = await db.MediaItems.CountAsync(stoppingToken);
                            }

                            int newItemsCount = itemsAfterScan - itemsBeforeScan;
                            _logger.LogInformation("Scan aller Quellen abgeschlossen. {NewItems} neue Items gefunden.", newItemsCount);

                            // Sende SignalR-Update �ber NotificationService
                            await _notificationService.NotifyNewVideosScannedAsync(0L, newItemsCount, stoppingToken);

                            _logger.LogInformation("Pr�fe, ob Genres neu geladen werden m�ssen.");
                            await classifier.CheckReloadGenres(stoppingToken);

                            const int maxCollectionsPerRun = 64;
                            var scannedCollections = 0;

                            while (scannedCollections < maxCollectionsPerRun && await scanner.ScanNextMediaCollection(stoppingToken))
                            {
                                scannedCollections++;
                                _logger.LogInformation("Eine Collection wurde gescannt (ScanNextMediaCollection gab 'true' zur�ck).");
                            }

                            if (scannedCollections > 0)
                            {
                                _logger.LogInformation("Klassifiziere MediaItems nach ScanNextMediaCollection.");
                                await classifier.ClassifyMediaItemsAsync(stoppingToken);
                                _logger.LogInformation("Klassifizierung abgeschlossen.");

							// If scanning produced (or updated) classifyable collections, run collection-classification as well.
							// This is required to create/update TV show seasons/episodes from newly scanned season folders.
							int unclassifiedBefore;
							{
								var db = innerScope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
								unclassifiedBefore = await db.MediaCollections
									.CountAsync(mc => mc.Classifyable && (mc.Changed || !mc.ClassifiedAt.HasValue), stoppingToken);
							}

							if (unclassifiedBefore > 0)
							{
								_logger.LogInformation("Scan abgeschlossen. Starte Klassifizierung ({UnclassifiedCount} unclassifizierte Collections).", unclassifiedBefore);
								await classifier.ClassifyMediaCollectionsAsync(stoppingToken);
								_logger.LogInformation("Klassifizierung der Collections abgeschlossen.");
								_logger.LogInformation("Klassifizierung abgeschlossen.");

								// Sende SignalR-Update �ber NotificationService
								await _notificationService.NotifyNewVideosScannedAsync(0L, unclassifiedBefore, stoppingToken);
							}
                            }
                            else
                            {
                                // Z�hle unclassifizierte Items vor Klassifizierung
                                int unclassifiedBefore;
                                {
                                    var db = innerScope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                                    unclassifiedBefore = await db.MediaCollections
                                        .CountAsync(mc => mc.Classifyable && (mc.Changed || !mc.ClassifiedAt.HasValue), stoppingToken);
                                }

                                _logger.LogInformation("Scan abgeschlossen. Starte Klassifizierung ({UnclassifiedCount} unclassifizierte Collections).", unclassifiedBefore);
                                await classifier.ClassifyMediaCollectionsAsync(stoppingToken);
                                _logger.LogInformation("Klassifizierung der Collections abgeschlossen.");
                                _logger.LogInformation("Klassifizierung abgeschlossen.");

                                // Sende SignalR-Update �ber NotificationService
                                await _notificationService.NotifyNewVideosScannedAsync(0L, unclassifiedBefore, stoppingToken);
                            }

                            lastRun = now;
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fehler im MediaSourceScanService w�hrend Scan/Klassifizierung.");
                    }

                    if (_loopDelay > TimeSpan.Zero)
                    {
                        // Keep a small delay so we can pick up config changes reasonably quickly.
                        await Task.Delay(_loopDelay, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Fehler im MediaSourceScanService beim Start.");
            }
        }
    }
}