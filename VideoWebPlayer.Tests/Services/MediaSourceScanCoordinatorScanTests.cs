using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Hubs;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Prüft Scan, Benachrichtigung, Genre-Reload, Cap und Klassifizierung von
/// <see cref="MediaSourceScanCoordinator.RunFullScanAsync"/> und
/// <see cref="MediaSourceScanCoordinator.RescanCollectionTreeAsync"/>.
/// </summary>
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class MediaSourceScanCoordinatorScanTests : IDisposable
{
    private readonly string _rootDir;

    public MediaSourceScanCoordinatorScanTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-coordinator-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task RunFullScanAsync_NotifiesNewVideos_WithCountedDelta()
    {
        var ct = TestContext.Current.CancellationToken;
        File.WriteAllText(Path.Combine(_rootDir, "movie.mp4"), "video");
        File.WriteAllText(Path.Combine(_rootDir, "movie.nfo"), "<movie><title>Delta Movie</title></movie>");

        var connectionString = $"Data Source=file:coordinator-scan-delta-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var hubContext = new RecordingHubContext();
        var notificationService = new MediaUpdateNotificationService(hubContext, NullLogger<MediaUpdateNotificationService>.Instance);

        var serviceProvider = MediaSourceScanCoordinatorTestServices.Build(
            connectionString, new ConcurrentQueue<string>(), notificationService: notificationService);
        await MediaSourceScanCoordinatorTestServices.SeedAsync(serviceProvider, _rootDir, genresChanged: false, ct);

        using var scope = serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
        await coordinator.RunFullScanAsync("Automatischer Scanprozess", 64, ct);

        // `NotifyNewVideosScannedAsync` unterdrückt Aufrufe mit Count <= 0 (MediaUpdateNotificationService);
        // der einzige tatsächlich gesendete Aufruf trägt die gezählte Differenz als positiven Count.
        var call = Assert.Single(hubContext.Clients.Calls, c => c.Method == "NewVideosScanned");
        Assert.Equal(0L, call.Args[0]);
        Assert.True((int)call.Args[1]! > 0, "Die Benachrichtigung muss eine positive gezählte Differenz tragen.");
    }

    [Fact]
    public async Task RunFullScanAsync_RunsGenreReloadBeforeCollectionScan()
    {
        var ct = TestContext.Current.CancellationToken;
        // Leeres Quellverzeichnis: die Collection-Schleife findet nichts zu scannen.

        var connectionString = $"Data Source=file:coordinator-scan-genres-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var serviceProvider = MediaSourceScanCoordinatorTestServices.Build(connectionString, new ConcurrentQueue<string>());
        var sourceId = await MediaSourceScanCoordinatorTestServices.SeedAsync(serviceProvider, _rootDir, genresChanged: true, ct);

        using (var seedScope = serviceProvider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Movies.Add(new Movie { Name = "Genre Movie", MediaSourceId = sourceId, GenreNames = "Action", IsManuallyEdited = false, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }

        using var scope = serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
        var result = await coordinator.RunFullScanAsync("Automatischer Scanprozess", 64, ct);

        // Die leere Root-Collection selbst ist fällig und wird einmal gescannt; entscheidend für
        // diesen Test ist nicht ihre Anzahl, sondern dass der Genre-Reload unabhängig davon lief.
        Assert.Equal(1, result.CollectionsScanned);

        using var finalScope = serviceProvider.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False((await finalDb.Setups.SingleAsync(ct)).GenresChanged);
        var movie = await finalDb.Movies.Include(m => m.MovieGenres).SingleAsync(ct);
        Assert.NotEmpty(movie.MovieGenres);
    }

    [Fact]
    public async Task RunFullScanAsync_RespectsCap_WhenProvided()
    {
        var ct = TestContext.Current.CancellationToken;

        Directory.CreateDirectory(Path.Combine(_rootDir, "A"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "B"));
        File.WriteAllText(Path.Combine(_rootDir, "A", "a.mp4"), "video");
        File.WriteAllText(Path.Combine(_rootDir, "B", "b.mp4"), "video");

        var cappedConnectionString = $"Data Source=file:coordinator-scan-cap-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var cappedKeeper = new SqliteConnection(cappedConnectionString);
        cappedKeeper.Open();
        var cappedServiceProvider = MediaSourceScanCoordinatorTestServices.Build(cappedConnectionString, new ConcurrentQueue<string>());
        await MediaSourceScanCoordinatorTestServices.SeedAsync(cappedServiceProvider, _rootDir, genresChanged: false, ct);

        using (var scope = cappedServiceProvider.CreateScope())
        {
            var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
            var cappedResult = await coordinator.RunFullScanAsync("Automatischer Scanprozess", 1, ct);
            Assert.Equal(1, cappedResult.CollectionsScanned);
            Assert.True(cappedResult.CapReached);
        }

        var uncappedConnectionString = $"Data Source=file:coordinator-scan-uncapped-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var uncappedKeeper = new SqliteConnection(uncappedConnectionString);
        uncappedKeeper.Open();
        var uncappedServiceProvider = MediaSourceScanCoordinatorTestServices.Build(uncappedConnectionString, new ConcurrentQueue<string>());
        await MediaSourceScanCoordinatorTestServices.SeedAsync(uncappedServiceProvider, _rootDir, genresChanged: false, ct);

        using (var scope = uncappedServiceProvider.CreateScope())
        {
            var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
            var uncappedResult = await coordinator.RunFullScanAsync("Automatischer Scanprozess", null, ct);
            Assert.Equal(3, uncappedResult.CollectionsScanned);
            Assert.False(uncappedResult.CapReached);
        }
    }

    [Fact]
    public async Task RescanCollectionTreeAsync_ScansOnlySubtree()
    {
        var ct = TestContext.Current.CancellationToken;

        var dirA = Path.Combine(_rootDir, "SeriesA");
        var dirB = Path.Combine(_rootDir, "SeriesB");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        File.WriteAllText(Path.Combine(dirA, "a1.mp4"), "video");
        File.WriteAllText(Path.Combine(dirB, "b1.mp4"), "video");

        var connectionString = $"Data Source=file:coordinator-scan-subtree-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var serviceProvider = MediaSourceScanCoordinatorTestServices.Build(connectionString, new ConcurrentQueue<string>());
        await MediaSourceScanCoordinatorTestServices.SeedAsync(serviceProvider, _rootDir, genresChanged: false, ct);

        using (var scope = serviceProvider.CreateScope())
        {
            var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
            await coordinator.RunFullScanAsync("Automatischer Scanprozess", null, ct);
        }

        long collectionAId, collectionBId;
        int itemsBInitially;
        DateTime? lastScannedAtBInitially;
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var collectionA = await db.MediaCollections.SingleAsync(c => c.Path == dirA, ct);
            var collectionB = await db.MediaCollections.SingleAsync(c => c.Path == dirB, ct);
            collectionAId = collectionA.Id;
            collectionBId = collectionB.Id;
            lastScannedAtBInitially = collectionB.LastScannedAt;
            itemsBInitially = await db.MediaItems.CountAsync(i => i.MediaCollectionId == collectionBId, ct);
        }

        File.WriteAllText(Path.Combine(dirA, "a2.mp4"), "video");

        using (var scope = serviceProvider.CreateScope())
        {
            var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
            var result = await coordinator.RescanCollectionTreeAsync(collectionAId, "Manueller Collection-Scan", ct);
            Assert.Equal(1, result.NewItemsCount);
        }

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await db.MediaItems.CountAsync(i => i.MediaCollectionId == collectionAId, ct));
            Assert.Equal(itemsBInitially, await db.MediaItems.CountAsync(i => i.MediaCollectionId == collectionBId, ct));
            var collectionB = await db.MediaCollections.SingleAsync(c => c.Id == collectionBId, ct);
            Assert.Equal(lastScannedAtBInitially, collectionB.LastScannedAt);
        }
    }

    [Fact]
    public async Task RescanCollectionTreeAsync_ReportsDeferredClassification()
    {
        var ct = TestContext.Current.CancellationToken;
        File.WriteAllText(Path.Combine(_rootDir, "movie.mp4"), "video");
        File.WriteAllText(Path.Combine(_rootDir, "movie.nfo"), "<movie><title>Deferred Movie</title></movie>");

        var connectionString = $"Data Source=file:coordinator-scan-deferred-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var gatedWriteCoordinator = new GatedMetadataWriteCoordinator();
        var serviceProvider = MediaSourceScanCoordinatorTestServices.Build(
            connectionString, new ConcurrentQueue<string>(), writeCoordinator: gatedWriteCoordinator);
        await MediaSourceScanCoordinatorTestServices.SeedAsync(serviceProvider, _rootDir, genresChanged: false, ct);

        long rootCollectionId;
        using (var seedScanScope = serviceProvider.CreateScope())
        {
            var scanner = seedScanScope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
            await scanner.ScanAllSourcesAsync(ct);
            var db = seedScanScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            rootCollectionId = (await db.MediaCollections.SingleAsync(c => c.Path == _rootDir, ct)).Id;
        }

        gatedWriteCoordinator.Hold();

        // Belegt die statische Klassifizierungssperre über eine beliebige (hier dieselbe) Collection,
        // während der Schreib-Lease zurückgehalten wird.
        using var occupyingScope = serviceProvider.CreateScope();
        var occupyingClassifier = occupyingScope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();
        var occupyingTask = occupyingClassifier.ClassifyCollectionTreeAsync(rootCollectionId, ct);

        await gatedWriteCoordinator.WaitUntilEnteredAsync(ct);

        using var scope = serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
        var result = await coordinator.RescanCollectionTreeAsync(rootCollectionId, "Manueller Collection-Scan", ct);

        Assert.True(result.ClassificationDeferred);

        gatedWriteCoordinator.Release();
        await occupyingTask;

        using var finalScope = serviceProvider.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var movie = await finalDb.Movies.SingleOrDefaultAsync(ct);
        Assert.NotNull(movie);
    }

    [Fact]
    public async Task RescanCollectionTreeAsync_MissingCollection_ReturnsStatusInsteadOfThrowing()
    {
        var ct = TestContext.Current.CancellationToken;

        var connectionString = $"Data Source=file:coordinator-scan-missing-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var serviceProvider = MediaSourceScanCoordinatorTestServices.Build(connectionString, new ConcurrentQueue<string>());
        await MediaSourceScanCoordinatorTestServices.SeedAsync(serviceProvider, _rootDir, genresChanged: false, ct);

        using var scope = serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();

        var result = await coordinator.RescanCollectionTreeAsync(999_999, "Manueller Collection-Scan", ct);

        Assert.Equal(0, result.NewItemsCount);
        Assert.False(result.ClassificationDeferred);
    }

    private sealed class RecordingHubContext : IHubContext<MediaUpdateHub>
    {
        public RecordingHubClients Clients { get; } = new();

        IHubClients IHubContext<MediaUpdateHub>.Clients => Clients;

        public IGroupManager Groups => throw new NotImplementedException();
    }

    private sealed class RecordingHubClients : IHubClients
    {
        private readonly RecordingClientProxy _proxy = new();

        public List<(string Method, object?[] Args)> Calls => _proxy.Calls;

        public IClientProxy All => _proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Client(string connectionId) => _proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
        public IClientProxy Group(string groupName) => _proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
        public IClientProxy User(string userId) => _proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Calls { get; } = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Calls.Add((method, args));
            return Task.CompletedTask;
        }
    }
}
