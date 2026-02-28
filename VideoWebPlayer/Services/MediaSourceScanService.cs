using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _loopDelay;
        private readonly bool _skipUpgrade;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceScanService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider used to create scopes.</param>
        /// <param name="eventManager">Event manager instance.</param>
        /// <param name="logger">Logger instance.</param>
        public MediaSourceScanService(
            IServiceProvider serviceProvider,
            EventManager eventManager,
            ILogger<MediaSourceScanService> logger,
            TimeSpan? initialDelay = null,
            TimeSpan? loopDelay = null,
            bool skipUpgrade = false,
            TimeProvider? timeProvider = null)
        {
            _serviceProvider = serviceProvider;
            _eventManager = eventManager;
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
                            await scanner.ScanAllSourcesAsync(stoppingToken);
                            _logger.LogInformation("Scan aller Quellen abgeschlossen.");
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
                                _logger.LogInformation("Scan abgeschlossen. Starte Klassifizierung.");
                                await classifier.ClassifyAllAsync(stoppingToken);
                                _logger.LogInformation("Klassifizierung abgeschlossen.");
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