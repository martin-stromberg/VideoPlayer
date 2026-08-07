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

public class MediaSourceScannerTests
{
    [Fact]
    public async Task ScanNextMediaCollection_SkipsCollection_WhenRemoteDirectoryIsMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:scanner-tests-missing-directory?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var missingPath = "/media/Filme/Es/.actors";
        var otherPath = "/media/Filme/Es";
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new IncrementingTimeProvider(start, TimeSpan.Zero);

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<SftpMediaSourceReader>(new ThrowingSftpMediaSourceReader(missingPath));
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

        var source = new MediaSource
        {
            Name = "Test Source",
            Path = "/media",
            Host = "localhost",
            Port = 22,
            CreatedAt = start.UtcDateTime
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var dueAt = start.UtcDateTime.AddDays(-1);
        db.MediaCollections.Add(new MediaCollection
        {
            Name = ".actors",
            Path = missingPath,
            CreatedAt = start.UtcDateTime,
            MediaSource = source,
            MediaSourceId = source.Id,
            ScanDueAt = dueAt
        });
        db.MediaCollections.Add(new MediaCollection
        {
            Name = "Es",
            Path = otherPath,
            CreatedAt = start.UtcDateTime,
            MediaSource = source,
            MediaSourceId = source.Id,
            ScanDueAt = dueAt.AddMinutes(1)
        });
        await db.SaveChangesAsync(ct);

        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();

        Assert.True(await scanner.ScanNextMediaCollection(ct));
        Assert.True(await scanner.ScanNextMediaCollection(ct));
        Assert.False(await scanner.ScanNextMediaCollection(ct));

        var missing = await db.MediaCollections.SingleAsync(c => c.Path == missingPath, ct);
        Assert.True(missing.ScanDueAt > start.UtcDateTime);
    }
}
