using Microsoft.Extensions.Logging;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

public sealed class TestableMediaSourceScanService : MediaSourceScanService
{
    public TestableMediaSourceScanService(
        IServiceProvider serviceProvider,
        EventManager eventManager,
        ILogger<MediaSourceScanService> logger,
        TimeSpan? initialDelay,
        TimeSpan? loopDelay,
        bool skipUpgrade,
        TimeProvider? timeProvider)
        : base(serviceProvider, eventManager, logger, initialDelay, loopDelay, skipUpgrade, timeProvider)
    {
    }

    public Task RunAsync(CancellationToken stoppingToken)
    {
        return ExecuteAsync(stoppingToken);
    }
}
