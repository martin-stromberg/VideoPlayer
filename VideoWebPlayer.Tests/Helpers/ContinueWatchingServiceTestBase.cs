using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VideoWebPlayer.Data;
using VideoWebPlayer.Hubs;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Stellt eine <see cref="ContinueWatchingService"/>-Instanz mit gemocktem SignalR-Hub
/// und In-Memory-Datenbank für Tests bereit.
/// </summary>
public abstract class ContinueWatchingServiceTestBase
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan CompletedPosition = TimeSpan.FromMinutes(45) - TimeSpan.FromSeconds(10);

    protected readonly ApplicationDbContext _db;
    protected readonly Mock<IHubContext<MediaUpdateHub>> _mockHubContext;
    protected readonly Mock<IHubClients> _mockClients;
    protected readonly Mock<IClientProxy> _mockClientProxy;
    protected readonly MediaUpdateNotificationService _notificationService;
    protected readonly ContinueWatchingService _service;
    protected readonly string _testUserId = "test-user-123";
    protected readonly List<string> _signalRCallLog = new();

    protected ContinueWatchingServiceTestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var eventManager = new EventManager();
        _db = new ApplicationDbContext(options, eventManager);

        _mockClientProxy = new Mock<IClientProxy>();
        _mockClientProxy
            .Setup(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, ct) =>
            {
                var logEntry = $"{method}({string.Join(", ", args?.Select(a => a?.ToString() ?? "null") ?? Array.Empty<string>())})";
                _signalRCallLog.Add(logEntry);
                Console.WriteLine($"[SignalR Mock] {logEntry}");
            })
            .Returns(Task.CompletedTask);

        _mockClients = new Mock<IHubClients>();
        _mockClients.Setup(x => x.User(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        _mockClients.Setup(x => x.All).Returns(_mockClientProxy.Object);

        _mockHubContext = new Mock<IHubContext<MediaUpdateHub>>();
        _mockHubContext.Setup(x => x.Clients).Returns(_mockClients.Object);

        var notificationLogger = Mock.Of<ILogger<MediaUpdateNotificationService>>();
        _notificationService = new MediaUpdateNotificationService(_mockHubContext.Object, notificationLogger);

        var userManager = CreateMockUserManager();
        var logger = Mock.Of<ILogger<ContinueWatchingService>>();
        var buffer = new ContinueWatchingBuffer();
        var programSettings = new ProgramSettingsService(_db, Mock.Of<ILogger<ProgramSettingsService>>());

        _service = new ContinueWatchingService(
            _db,
            userManager,
            logger,
            buffer,
            _notificationService,
            programSettings);
    }

    private static UserManager<ApplicationUser> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        return mockUserManager.Object;
    }
}
