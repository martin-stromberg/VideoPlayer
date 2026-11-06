using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using VideoWebPlayer.Services;
using VideoWebPlayer.Hubs;

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
        : base(serviceProvider, eventManager, serviceProvider.GetRequiredService<MediaUpdateNotificationService>(), logger, initialDelay, loopDelay, skipUpgrade, timeProvider)
    {
    }

    public Task RunAsync(CancellationToken stoppingToken)
    {
        return ExecuteAsync(stoppingToken);
    }

    /// <summary>
    /// Erzeugt einen <see cref="MediaUpdateNotificationService"/> mit einem Null-Hub, damit
    /// Benachrichtigungen in Tests ohne echten SignalR-Hub laufen.
    /// </summary>
    /// <returns>Eine Instanz, die SignalR-Aufrufe ins Leere laufen lässt.</returns>
    public static MediaUpdateNotificationService CreateNullNotificationService()
    {
        var hubContext = new NullHubContext();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaUpdateNotificationService>.Instance;
        return new MediaUpdateNotificationService(hubContext, logger);
    }

    /// <summary>
    /// Null implementation of IHubContext for testing.
    /// </summary>
    private class NullHubContext : IHubContext<MediaUpdateHub>
    {
        public IHubClients Clients => new NullHubClients();
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private class NullHubClients : IHubClients
    {
        public IClientProxy All => new NullClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new NullClientProxy();
        public IClientProxy Client(string connectionId) => new NullClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new NullClientProxy();
        public IClientProxy Group(string groupName) => new NullClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new NullClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new NullClientProxy();
        public IClientProxy User(string userId) => new NullClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new NullClientProxy();
    }

    private class NullClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
