using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Background worker that backfills actor metadata for existing movies and episodes on application start.
    /// </summary>
    public class ActorBackfillWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ActorBackfillWorker> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorBackfillWorker"/> class.
        /// </summary>
        public ActorBackfillWorker(IServiceScopeFactory scopeFactory, ILogger<ActorBackfillWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ActorBackfillWorker] Started");

            await Task.Yield();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();
                var gate = scope.ServiceProvider.GetService<IBackgroundProcessingGate>();

                await using var processingLease = gate is null ? null : await gate.EnterOperationAsync("ActorBackfill", stoppingToken);
                await classifier.BackfillMissingActorsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ActorBackfillWorker] Fehler bei der Nacherfassung der Schauspieler.");
            }

            _logger.LogInformation("[ActorBackfillWorker] Finished; idling.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
