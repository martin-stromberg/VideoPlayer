using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using msTools.Updater;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Updates;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class UpdateSettingsServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_UsesConfiguredDefaults()
    {
        await using var db = CreateDb();
        var options = new AutoUpdateOptions();
        var service = CreateService(db, options, new Dictionary<string, string?>
        {
            ["AutoUpdate:Enabled"] = "false",
            ["AutoUpdate:SourceCheck:Interval"] = "30",
            ["AutoUpdate:AllowPrereleaseUpdates"] = "true",
            ["AutoUpdate:EnableAutomaticInstallation"] = "true",
            ["AutoUpdate:EnableAutomaticDownload"] = "false",
            ["AutoUpdate:ServiceName"] = " video-web ",
            ["AutoUpdate:Backup:Enabled"] = "true",
            ["AutoUpdate:Backup:Path"] = "Data/UpdateBackups",
            ["AutoUpdate:Backup:RetainedBackupCount"] = "2",
            ["AutoUpdate:Backup:CancelInstallationOnFailure"] = "false"
        });

        var settings = await service.GetOrCreateAsync(TestContext.Current.CancellationToken);

        Assert.False(settings.AutomaticChecksEnabled);
        Assert.Equal(30, settings.CheckIntervalMinutes);
        Assert.True(settings.AllowPrereleaseUpdates);
        Assert.True(settings.AutomaticInstallationEnabled);
        Assert.False(settings.AutomaticDownloadEnabled);
        Assert.Equal("video-web", settings.ServiceName);
        Assert.Equal("Data/UpdateBackups", settings.UpdateBackupPath);
        Assert.Equal(2, settings.RetainedUpdateBackupCount);
        Assert.False(settings.CancelInstallationOnBackupFailure);
    }

    [Fact]
    public async Task UpdateAsync_ClampsValuesAndAppliesRuntimeOptions()
    {
        await using var db = CreateDb();
        var options = new AutoUpdateOptions();
        var service = CreateService(db, options);

        await service.UpdateAsync(new UpdateSettingsUpdate(
            AutomaticChecksEnabled: true,
            CheckIntervalMinutes: 0,
            AllowPrereleaseUpdates: true,
            AutomaticInstallationEnabled: true,
            AutomaticDownloadEnabled: false,
            ServiceName: " service ",
            CreateBackupBeforeInstallation: true,
            CancelInstallationOnBackupFailure: true,
            UpdateBackupPath: " Updates/Backups ",
            RetainedUpdateBackupCount: -4),
            TestContext.Current.CancellationToken);

        var persisted = await db.UpdateSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, persisted.CheckIntervalMinutes);
        Assert.Equal(0, persisted.RetainedUpdateBackupCount);
        Assert.True(persisted.AutomaticDownloadEnabled);
        Assert.Equal("service", persisted.ServiceName);

        Assert.True(options.Enabled);
        Assert.Equal(1, options.SourceCheck.Interval);
        Assert.True(options.AllowPrereleaseUpdates);
        Assert.True(options.EnableAutomaticInstallation);
        Assert.True(options.EnableAutomaticDownload);
        Assert.Equal("service", options.ServiceName);
        Assert.NotNull(options.Source);
    }

    [Fact]
    public async Task ApplyToRuntimeOptions_WhenAutomaticChecksDisabled_KeepsUpdaterEnabledForManualActions()
    {
        await using var db = CreateDb();
        var options = new AutoUpdateOptions { Enabled = false };
        var service = CreateService(db, options);

        await service.UpdateAsync(new UpdateSettingsUpdate(
            AutomaticChecksEnabled: false,
            CheckIntervalMinutes: 15,
            AllowPrereleaseUpdates: false,
            AutomaticInstallationEnabled: false,
            AutomaticDownloadEnabled: true,
            ServiceName: null,
            CreateBackupBeforeInstallation: true,
            CancelInstallationOnBackupFailure: true,
            UpdateBackupPath: null,
            RetainedUpdateBackupCount: 5),
            TestContext.Current.CancellationToken);

        Assert.True(options.Enabled);
        Assert.Equal(15, options.SourceCheck.Interval);
        Assert.NotEmpty(options.SourceCheck.TimeRanges);
        Assert.False(new SourceCheckWindowEvaluator().IsWithinWindow(
            options.SourceCheck.TimeRanges,
            new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task GetBackupOptionsAsync_ReturnsPersistedUpdateBackupSettings()
    {
        await using var db = CreateDb();
        var service = CreateService(db, new AutoUpdateOptions());

        await service.UpdateAsync(new UpdateSettingsUpdate(
            AutomaticChecksEnabled: true,
            CheckIntervalMinutes: 10,
            AllowPrereleaseUpdates: false,
            AutomaticInstallationEnabled: false,
            AutomaticDownloadEnabled: true,
            ServiceName: null,
            CreateBackupBeforeInstallation: false,
            CancelInstallationOnBackupFailure: false,
            UpdateBackupPath: "UpdateBackups",
            RetainedUpdateBackupCount: 8),
            TestContext.Current.CancellationToken);

        var backupOptions = await service.GetBackupOptionsAsync(TestContext.Current.CancellationToken);

        Assert.False(backupOptions.Enabled);
        Assert.False(backupOptions.CancelInstallationOnFailure);
        Assert.Equal("UpdateBackups", backupOptions.Path);
        Assert.Equal(8, backupOptions.RetainedBackupCount);
    }

    private static ApplicationDbContext CreateDb()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"update-settings-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(dbOptions, new EventManager());
    }

    private static UpdateSettingsService CreateService(
        ApplicationDbContext db,
        AutoUpdateOptions options,
        Dictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

        return new UpdateSettingsService(
            db,
            configuration,
            options,
            new VideoWebPlayerUpdateSourceFactory(configuration));
    }
}
