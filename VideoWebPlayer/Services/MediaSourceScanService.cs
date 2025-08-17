using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace VideoWebPlayer.Services
{
    public class MediaSourceScanService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly EventManager _eventManager;
        private readonly ILogger<MediaSourceScanService> _logger;

        public MediaSourceScanService(IServiceProvider serviceProvider, EventManager eventManager, ILogger<MediaSourceScanService> logger)
        {
            _serviceProvider = serviceProvider;
            _eventManager = eventManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var upgradeManager = scope.ServiceProvider.GetRequiredService<DataUpgradeManager>();
                    _logger.LogInformation("Starte Datenbank-Upgrade.");
                    await upgradeManager.EnsureUpToDateAsync(stoppingToken);
                    _logger.LogInformation("Datenbank-Upgrade abgeschlossen.");
                }
                _logger.LogInformation("Warte 10 Sekunden.");
                await Task.Delay(10000);
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
                        var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

                        await classifier.CheckReloadGenres(stoppingToken);
                        _logger.LogInformation("Starte Scan aller Quellen.");
                        await scanner.ScanAllSourcesAsync(stoppingToken);
                        _logger.LogInformation("Scan abgeschlossen. Starte Klassifizierung.");
                        await classifier.ClassifyAllAsync(stoppingToken);
                        _logger.LogInformation("Klassifizierung abgeschlossen.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fehler im MediaSourceScanService während Scan/Klassifizierung.");
                    }
                    _logger.LogInformation("Warte 1 Stunde.");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Fehler im MediaSourceScanService beim Start.");
            }
        }
    }
}