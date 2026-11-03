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
        /// <param name="initialDelay">Optional delay before the first scan loop iteration.</param>
        /// <param name="loopDelay">Optional delay between scan loop iterations.</param>
        /// <param name="skipUpgrade">Whether to skip the startup upgrade/backfill step.</param>
        /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
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
                        var settings = settingsScope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
                        var (scanProcessInterval, _) = await settings.GetScanIntervalsAsync(stoppingToken);

                        if (scanProcessInterval <= TimeSpan.Zero)
                            scanProcessInterval = TimeSpan.FromHours(1);

                        if (now - lastRun >= scanProcessInterval)
                        {
                            lastRun = now;
                            _logger.LogInformation("Starte Scanprozess (Intervall: {Interval}).", scanProcessInterval);

                            var coordinator = innerScope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
                            await coordinator.RunFullScanAsync("Automatischer Scanprozess", 64, stoppingToken);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im MediaSourceScanService beim Start.");
            }
        }
    }
}