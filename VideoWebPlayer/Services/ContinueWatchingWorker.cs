using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace VideoWebPlayer.Services
{
    public class ContinueWatchingWorker : BackgroundService
    {
        private readonly ContinueWatchingBuffer _buffer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ContinueWatchingWorker> _logger;

        public ContinueWatchingWorker(ContinueWatchingBuffer buffer, IServiceScopeFactory scopeFactory, ILogger<ContinueWatchingWorker> logger)
        {
            _buffer = buffer;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var entry = await _buffer.ReadNextAsync(stoppingToken);
                    if (entry is null) continue;

                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ContinueWatchingService>();

                    await service.ProcessBufferedEntryAsync(entry.UserId, entry.MovieId, entry.EpisodeId, entry.Position, entry.Duration, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler im ContinueWatchingWorker bei der Verarbeitung eines Eintrags.");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
    }
}