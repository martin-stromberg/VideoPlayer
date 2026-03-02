using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Background worker that persists buffered continue-watching progress entries.
    /// </summary>
    public class ContinueWatchingWorker : BackgroundService
    {
        private readonly ContinueWatchingBuffer _buffer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ContinueWatchingWorker> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContinueWatchingWorker"/> class.
        /// </summary>
        /// <param name="buffer">The progress buffer.</param>
        /// <param name="scopeFactory">Scope factory for resolving services.</param>
        /// <param name="logger">Logger instance.</param>
        public ContinueWatchingWorker(ContinueWatchingBuffer buffer, IServiceScopeFactory scopeFactory, ILogger<ContinueWatchingWorker> logger)
        {
            _buffer = buffer;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Executes the background processing loop.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to stop processing.</param>
        /// <returns>A task that represents the background operation.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ContinueWatchingWorker] Started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("[ContinueWatchingWorker] Waiting for next entry...");
                    
                    var entry = await _buffer.ReadNextAsync(stoppingToken);
                    
                    if (entry is null)
                    {
                        _logger.LogDebug("[ContinueWatchingWorker] Received null entry (duplicate key) - skipping");
                        continue;
                    }

                    _logger.LogInformation(
                        "[ContinueWatchingWorker] Processing entry for user {UserId}, Movie: {MovieId}, Episode: {EpisodeId}, Position: {Position}s",
                        entry.UserId, entry.MovieId, entry.EpisodeId, entry.Position.TotalSeconds);

                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ContinueWatchingService>();

                    await service.ProcessBufferedEntryAsync(entry.UserId, entry.MovieId, entry.EpisodeId, entry.Position, entry.Duration, stoppingToken);
                    
                    _logger.LogDebug("[ContinueWatchingWorker] Entry processed successfully");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("[ContinueWatchingWorker] Stopping (cancellation requested)");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ContinueWatchingWorker] Fehler bei der Verarbeitung eines Eintrags.");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            
            _logger.LogInformation("[ContinueWatchingWorker] Stopped");
        }
    }
}