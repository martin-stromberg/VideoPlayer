using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VideoWebPlayer.Data;
using VideoWebPlayer.Hubs;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests für ContinueWatchingService SignalR-Integration.
/// </summary>
public class ContinueWatchingServiceSignalRTests
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IHubContext<MediaUpdateHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly MediaUpdateNotificationService _notificationService;
    private readonly ContinueWatchingService _service;
    private readonly string _testUserId = "test-user-123";
    private readonly List<string> _signalRCallLog = new();

    public ContinueWatchingServiceSignalRTests()
    {
        // In-Memory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var eventManager = new EventManager();
        _db = new ApplicationDbContext(options, eventManager);

        // Mock SignalR Hub Context
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

        // MediaUpdateNotificationService mit Mock
        var notificationLogger = Mock.Of<ILogger<MediaUpdateNotificationService>>();
        _notificationService = new MediaUpdateNotificationService(_mockHubContext.Object, notificationLogger);

        // ContinueWatchingService mit NotificationService
        var userManager = CreateMockUserManager();
        var logger = Mock.Of<ILogger<ContinueWatchingService>>();
        var buffer = new ContinueWatchingBuffer();

        _service = new ContinueWatchingService(
            _db,
            userManager,
            logger,
            buffer,
            _notificationService);
    }
    
    private static Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> CreateMockUserManager()
    {
        var store = new Mock<Microsoft.AspNetCore.Identity.IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        return mockUserManager.Object;
    }

    [Fact]
    public async Task ProcessBufferedEntry_NewEntry_SendsSignalRUpdate()
    {
        // Arrange
        var movieId = 123L;
        var position = TimeSpan.FromMinutes(15);
        var duration = TimeSpan.FromMinutes(90);

        // Act
        await _service.ProcessBufferedEntryAsync(_testUserId, movieId, null, position, duration);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "ContinueWatchingUpdated",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "SignalR-Event 'ContinueWatchingUpdated' sollte genau einmal gesendet werden");

        _mockClients.Verify(
            x => x.User(_testUserId),
            Times.Once,
            "Event sollte an den spezifischen User gesendet werden");

        Assert.Contains(_signalRCallLog, s => s.Contains("ContinueWatchingUpdated"));
    }

    [Fact]
    public async Task ProcessBufferedEntry_UpdateExisting_SendsSignalRUpdate()
    {
        // Arrange
        var movieId = 456L;
        var position1 = TimeSpan.FromMinutes(10);
        var position2 = TimeSpan.FromMinutes(20);
        var duration = TimeSpan.FromMinutes(90);

        // Act - Erste Position
        await _service.ProcessBufferedEntryAsync(_testUserId, movieId, null, position1, duration);
        
        // Act - Update Position
        _signalRCallLog.Clear(); // Reset für zweiten Test
        await _service.ProcessBufferedEntryAsync(_testUserId, movieId, null, position2, duration);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "ContinueWatchingUpdated",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "SignalR-Event sollte auch bei Update gesendet werden");

        Assert.Contains(_signalRCallLog, s => s.Contains("ContinueWatchingUpdated"));
    }

    [Fact]
    public async Task ProcessBufferedEntry_Episode_SendsSignalRUpdate()
    {
        // Arrange
        var episodeId = 789L;
        var position = TimeSpan.FromMinutes(5);
        var duration = TimeSpan.FromMinutes(45);

        // Erstelle TV Show Struktur in DB
        var tvShow = new TVShow { Name = "Test Show", MediaSourceId = 1 };
        _db.TVShows.Add(tvShow);
        await _db.SaveChangesAsync();

        var season = new TVShowSeason { TVShowId = tvShow.Id, Name = "Staffel 01", MediaSourceId = 1 };
        _db.TVShowSeasons.Add(season);
        await _db.SaveChangesAsync();

        var episode = new TVShowEpisode 
        { 
            Id = episodeId, 
            TVShowSeasonId = season.Id, 
            Name = "Test Episode", 
            Number = 1, 
            MediaSourceId = 1 
        };
        _db.TVShowEpisodes.Add(episode);
        await _db.SaveChangesAsync();

        // Act
        await _service.ProcessBufferedEntryAsync(_testUserId, null, episodeId, position, duration);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "ContinueWatchingUpdated",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "SignalR-Event sollte auch für Episoden gesendet werden");

        _mockClients.Verify(x => x.User(_testUserId), Times.Once);
        Assert.Contains(_signalRCallLog, s => s.Contains("ContinueWatchingUpdated"));
    }

    [Fact]
    public async Task MultipleUpdates_SendsMultipleEvents()
    {
        // Arrange
        var movieId1 = 111L;
        var movieId2 = 222L;
        var position = TimeSpan.FromMinutes(30);
        var duration = TimeSpan.FromMinutes(90);

        // Act
        await _service.ProcessBufferedEntryAsync(_testUserId, movieId1, null, position, duration);
        await _service.ProcessBufferedEntryAsync(_testUserId, movieId2, null, position, duration);
        await _service.ProcessBufferedEntryAsync(_testUserId, movieId1, null, position + TimeSpan.FromMinutes(5), duration);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "ContinueWatchingUpdated",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(3),
            "Jedes Update sollte ein SignalR-Event triggern");

        Assert.True(_signalRCallLog.Count(s => s.Contains("ContinueWatchingUpdated")) >= 3);
    }

    [Fact]
    public async Task BufferFlow_EnqueueAndProcess_SendsSignalREvent()
    {
        // Arrange
        var buffer = new ContinueWatchingBuffer();
        var movieId = 333L;
        var position = TimeSpan.FromMinutes(45);
        var duration = TimeSpan.FromMinutes(120);

        // Act - Schritt 1: Enqueue (simuliert API-Call)
        buffer.EnqueueOrUpdate(_testUserId, movieId, null, position, duration);

        // Act - Schritt 2: Worker liest Entry
        var entry = await buffer.ReadNextAsync(CancellationToken.None);
        Assert.NotNull(entry);
        Assert.Equal(_testUserId, entry.UserId);
        Assert.Equal(movieId, entry.MovieId);

        // Act - Schritt 3: Service verarbeitet Entry
        _signalRCallLog.Clear();
        await _service.ProcessBufferedEntryAsync(entry.UserId, entry.MovieId, entry.EpisodeId, entry.Position, entry.Duration);

        // Assert - SignalR-Event wurde gesendet
        Assert.Contains(_signalRCallLog, s => s.Contains("ContinueWatchingUpdated"));
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "ContinueWatchingUpdated",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
