using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using msTools.Updater;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Updates;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class UpdateSettingsServiceTests_Iis
{
    [Fact]
    public async Task UpdateAsync_UnderIis_DiscardsSubmittedServiceName()
    {
        await using var db = CreateDb();
        var options = new AutoUpdateOptions();
        var service = CreateService(db, options, isIisHosted: true);

        await service.UpdateAsync(new UpdateSettingsUpdate(
            AutomaticChecksEnabled: true,
            CheckIntervalMinutes: 12,
            AllowPrereleaseUpdates: false,
            AutomaticInstallationEnabled: false,
            AutomaticDownloadEnabled: true,
            ServiceName: "webplayer.service",
            CreateBackupBeforeInstallation: true,
            CancelInstallationOnBackupFailure: true,
            UpdateBackupPath: "UpdateBackups",
            RetainedUpdateBackupCount: 4),
            TestContext.Current.CancellationToken);

        var persisted = await db.UpdateSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(persisted.ServiceName);
        Assert.Null(options.ServiceName);
    }

    [Fact]
    public async Task ApplyToRuntimeOptions_UnderIis_DoesNotCopyPersistedServiceName()
    {
        await using var db = CreateDb();
        db.UpdateSettings.Add(new UpdateSettings
        {
            Id = 1,
            CheckIntervalMinutes = 60,
            ServiceName = "webplayer.service",
            CreateBackupBeforeInstallation = true,
            CancelInstallationOnBackupFailure = true,
            UpdateBackupPath = "Backups",
            RetainedUpdateBackupCount = 5
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var options = new AutoUpdateOptions { ServiceName = "webplayer.service" };
        var service = CreateService(db, options, isIisHosted: true);

        await service.ApplyToRuntimeOptionsAsync(TestContext.Current.CancellationToken);

        Assert.Null(options.ServiceName);
    }

    [Fact]
    public async Task GetOrCreateAsync_UnderIis_ClearsPersistedServiceName()
    {
        await using var db = CreateDb();
        db.UpdateSettings.Add(new UpdateSettings
        {
            Id = 1,
            CheckIntervalMinutes = 60,
            ServiceName = "webplayer.service",
            CreateBackupBeforeInstallation = true,
            CancelInstallationOnBackupFailure = true,
            UpdateBackupPath = "Backups",
            RetainedUpdateBackupCount = 5
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, new AutoUpdateOptions(), isIisHosted: true);

        var settings = await service.GetOrCreateAsync(TestContext.Current.CancellationToken);
        var persisted = await db.UpdateSettings.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Null(settings.ServiceName);
        Assert.Null(persisted.ServiceName);
    }

    [Fact]
    public async Task GetOrCreateAsync_UnderIis_SkipsConfiguredServiceNameDefault()
    {
        await using var db = CreateDb();
        var service = CreateService(db, new AutoUpdateOptions(), new Dictionary<string, string?>
        {
            ["AutoUpdate:ServiceName"] = "webplayer.service"
        }, isIisHosted: true);

        var settings = await service.GetOrCreateAsync(TestContext.Current.CancellationToken);

        Assert.Null(settings.ServiceName);
    }

    private static ApplicationDbContext CreateDb()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"update-settings-iis-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(dbOptions, new EventManager());
    }

    private static UpdateSettingsService CreateService(
        ApplicationDbContext db,
        AutoUpdateOptions options,
        Dictionary<string, string?>? values = null,
        bool isIisHosted = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

        return new UpdateSettingsService(
            db,
            configuration,
            options,
            new VideoWebPlayerUpdateSourceFactory(configuration),
            Mock.Of<IUpdateHostEnvironment>(x => x.RunsUnderIis == isIisHosted));
    }
}
