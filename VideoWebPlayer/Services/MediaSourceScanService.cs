using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;
using VideoWebPlayer.Hubs;

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
        private readonly IHubContext<MediaUpdateHub> _hubContext;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _loopDelay;
        private readonly bool _skipUpgrade;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceScanService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider used to create scopes.</param>
        /// <param name="eventManager">Event manager instance.</param>
        /// <param name="hubContext">SignalR hub context for push notifications.</param>
        /// <param name="logger">Logger instance.</param>
        public MediaSourceScanService(
            IServiceProvider serviceProvider,
            EventManager eventManager,
            IHubContext<MediaUpdateHub> hubContext,
            ILogger<MediaSourceScanService> logger,
            TimeSpan? initialDelay = null,
            TimeSpan? loopDelay = null,
            bool skipUpgrade = false,
            TimeProvider? timeProvider = null)
        {
            _serviceProvider = serviceProvider;
            _eventManager = eventManager;
            _hubContext = hubContext;
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
                var lastAllSourcesRun = DateTimeOffset.MinValue;
                var lastTenMinuteRun = DateTimeOffset.MinValue;
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var now = _timeProvider.GetUtcNow();
                        using var scope = _serviceProvider.CreateScope();
                        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
                        var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

                        if (now - lastAllSourcesRun >= TimeSpan.FromHours(1))
                        {
                            _logger.LogInformation("Starte Scan aller Quellen.");
                            
                            // Zähle Items vor dem Scan
                            int itemsBeforeScan = 0;
                            using (var countScope = _serviceProvider.CreateScope())
                            {
                                var db = countScope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                                itemsBeforeScan = await db.MediaItems.CountAsync(stoppingToken);
                            }
                            
                            await scanner.ScanAllSourcesAsync(stoppingToken);
                            
                            // Zähle Items nach dem Scan
                            int itemsAfterScan = 0;
                            using (var countScope = _serviceProvider.CreateScope())
                            {
                                var db = countScope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                                itemsAfterScan = await db.MediaItems.CountAsync(stoppingToken);
                            }
                            
                            int newItemsCount = itemsAfterScan - itemsBeforeScan;
                            _logger.LogInformation("Scan aller Quellen abgeschlossen. {NewItems} neue Items gefunden.", newItemsCount);
                            
                            // Sende SignalR-Update wenn neue Items gefunden wurden
                            if (newItemsCount > 0)
                            {
                                try
                                {
                                    await _hubContext.Clients.All
                                        .SendAsync("NewVideosScanned", 0L, newItemsCount, cancellationToken: stoppingToken);
                                    _logger.LogInformation("SignalR: NewVideosScanned sent to all clients (Count: {Count})", newItemsCount);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to send SignalR update for NewVideosScanned");
                                }
                            }
                            
                            lastAllSourcesRun = now;
                        }

                        if (now - lastTenMinuteRun >= TimeSpan.FromMinutes(10))
                        {
                            _logger.LogInformation("Prüfe, ob Genres neu geladen werden müssen.");
                            await classifier.CheckReloadGenres(stoppingToken);
                            
                            var foundCollection = await scanner.ScanNextMediaCollection(stoppingToken);
                            if (foundCollection)
                            {
                                _logger.LogInformation("Eine Collection wurde gescannt (ScanNextMediaCollection gab 'true' zurück).");
                            }
                            if (!foundCollection)
                            {
                                // Zähle unclassifizierte Items vor Klassifizierung
                                int unclassifiedBefore = 0;
                                using (var countScope = _serviceProvider.CreateScope())
                                {
                                    var db = countScope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                                    unclassifiedBefore = await db.MediaCollections
                                        .CountAsync(mc => mc.Classifyable && (mc.Changed || !mc.ClassifiedAt.HasValue), stoppingToken);
                                }
                                
                                _logger.LogInformation("Scan abgeschlossen. Starte Klassifizierung ({UnclassifiedCount} unclassifizierte Collections).", unclassifiedBefore);
                                await classifier.ClassifyAllAsync(stoppingToken);
                                _logger.LogInformation("Klassifizierung abgeschlossen.");
                                
                                // Sende SignalR-Update wenn neue Klassifizierungen durchgeführt wurden
                                if (unclassifiedBefore > 0)
                                {
                                    try
                                    {
                                        await _hubContext.Clients.All
                                            .SendAsync("NewVideosScanned", 0L, unclassifiedBefore, cancellationToken: stoppingToken);
                                        _logger.LogInformation("SignalR: NewVideosScanned sent after classification (Count: {Count})", unclassifiedBefore);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to send SignalR update after classification");
                                    }
                                }
                            }

                            lastTenMinuteRun = now;
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fehler im MediaSourceScanService während Scan/Klassifizierung.");
                    }

                    if (_loopDelay > TimeSpan.Zero)
                    {
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