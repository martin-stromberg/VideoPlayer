using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public class MediaSourceScannerLocalTests : IDisposable
{
    private readonly string _rootDir;

    public MediaSourceScannerLocalTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-scanner-local-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task ScanAllSourcesAsync_CreatesRootCollection_ForLocalSource()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateServiceProviderAsync("scanner-local-root", ct);
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource
        {
            Name = "Local Source",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        await scanner.ScanAllSourcesAsync(ct);

        var root = await db.MediaCollections.SingleAsync(c => c.MediaSourceId == source.Id, ct);
        Assert.Equal(_rootDir, root.Path);
        Assert.Equal(new DirectoryInfo(_rootDir).Name, root.Name);
        Assert.Equal(Directory.GetLastWriteTimeUtc(_rootDir), root.CreatedAt);
    }

    [Fact]
    public async Task ScanNextMediaCollection_PopulatesLocalDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        var subDir = Path.Combine(_rootDir, "Movies");
        Directory.CreateDirectory(subDir);
        var filePath = Path.Combine(_rootDir, "clip.mp4");
        File.WriteAllText(filePath, "video");

        await using var serviceProvider = await CreateServiceProviderAsync("scanner-local-entries", ct);
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource
        {
            Name = "Local Source",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var rootCollection = new MediaCollection
        {
            Name = "root",
            Path = _rootDir,
            CreatedAt = DateTime.UtcNow,
            MediaSource = source,
            MediaSourceId = source.Id,
            ScanDueAt = DateTime.UtcNow.AddDays(-1)
        };
        db.MediaCollections.Add(rootCollection);
        await db.SaveChangesAsync(ct);

        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        Assert.True(await scanner.ScanNextMediaCollection(ct));

        var childCollection = await db.MediaCollections.SingleAsync(c => c.ParentMediaCollectionId == rootCollection.Id, ct);
        Assert.Equal(subDir, childCollection.Path);
        Assert.Equal("Movies", childCollection.Name);

        var item = await db.MediaItems.SingleAsync(i => i.MediaCollectionId == rootCollection.Id, ct);
        Assert.Equal(filePath, item.Path);
        Assert.Equal("clip.mp4", item.Name);
        Assert.Equal(File.GetLastWriteTimeUtc(filePath), item.CreatedAt);
    }

    [Fact]
    public async Task ScanNextMediaCollection_SkipsCollection_WhenLocalDirectoryMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateServiceProviderAsync("scanner-local-missing", ct);
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var start = DateTime.UtcNow;
        var missingPath = Path.Combine(_rootDir, "missing");

        var source = new MediaSource
        {
            Name = "Local Source",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            CreatedAt = start
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var collection = new MediaCollection
        {
            Name = "missing",
            Path = missingPath,
            CreatedAt = start,
            MediaSource = source,
            MediaSourceId = source.Id,
            ScanDueAt = start.AddDays(-1)
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();

        Assert.True(await scanner.ScanNextMediaCollection(ct));
        Assert.False(await scanner.ScanNextMediaCollection(ct));

        var missing = await db.MediaCollections.SingleAsync(c => c.Path == missingPath, ct);
        Assert.NotNull(missing.ScanDueAt);
        Assert.True(missing.ScanDueAt > start);
    }

    private static async Task<ServiceProvider> CreateServiceProviderAsync(string dbName, CancellationToken ct)
    {
        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var services = new ServiceCollection();
        // Keeper-Connection im Provider registrieren, damit sie zusammen mit ihm disposed wird
        // und die Shared-In-Memory-Datenbank beim Testende geschlossen wird.
        services.AddSingleton(keeperConnection);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance));
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<MediaSourceScanner>();
        services.AddSingleton<IAuthService, TestAuthService>();
        services.AddSingleton<ILogger<MediaSourceScanner>>(NullLogger<MediaSourceScanner>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, GenresChanged = false });
        await db.SaveChangesAsync(ct);

        return serviceProvider;
    }
}
