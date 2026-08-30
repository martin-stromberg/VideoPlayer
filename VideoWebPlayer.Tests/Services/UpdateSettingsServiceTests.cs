using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    public async Task UpdateAsync_AppliesValidValuesToPersistenceAndRuntimeOptions()
    {
        await using var db = CreateDb();
        var options = new AutoUpdateOptions();
        var service = CreateService(db, options);

        await service.UpdateAsync(new UpdateSettingsUpdate(
            AutomaticChecksEnabled: true,
            CheckIntervalMinutes: 12,
            AllowPrereleaseUpdates: true,
            AutomaticInstallationEnabled: true,
            AutomaticDownloadEnabled: false,
            ServiceName: " service ",
            CreateBackupBeforeInstallation: true,
            CancelInstallationOnBackupFailure: true,
            UpdateBackupPath: " Updates/Backups ",
            RetainedUpdateBackupCount: 4),
            TestContext.Current.CancellationToken);

        var persisted = await db.UpdateSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(12, persisted.CheckIntervalMinutes);
        Assert.Equal(4, persisted.RetainedUpdateBackupCount);
        Assert.True(persisted.AutomaticDownloadEnabled);
        Assert.Equal("service", persisted.ServiceName);

        Assert.True(options.Enabled);
        Assert.Equal(12, options.SourceCheck.Interval);
        Assert.True(options.AllowPrereleaseUpdates);
        Assert.True(options.EnableAutomaticInstallation);
        Assert.True(options.EnableAutomaticDownload);
        Assert.Equal("service", options.ServiceName);
        Assert.NotNull(options.Source);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1_441, 5)]
    [InlineData(30, 0)]
    [InlineData(30, 11)]
    public async Task UpdateAsync_RejectsInvalidIntervalAndRetentionValues(int intervalMinutes, int retainedBackups)
    {
        await using var db = CreateDb();
        var options = new AutoUpdateOptions();
        var initialInterval = options.SourceCheck.Interval;
        var service = CreateService(db, options);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.UpdateAsync(new UpdateSettingsUpdate(
            AutomaticChecksEnabled: true,
            CheckIntervalMinutes: intervalMinutes,
            AllowPrereleaseUpdates: false,
            AutomaticInstallationEnabled: false,
            AutomaticDownloadEnabled: true,
            ServiceName: null,
            CreateBackupBeforeInstallation: true,
            CancelInstallationOnBackupFailure: true,
            UpdateBackupPath: "UpdateBackups",
            RetainedUpdateBackupCount: retainedBackups),
            TestContext.Current.CancellationToken));

        Assert.Empty(db.UpdateSettings);
        Assert.Equal(initialInterval, options.SourceCheck.Interval);
        Assert.False(options.AllowPrereleaseUpdates);
    }

    [Theory]
    [InlineData(0, 1, 0, 1)]
    [InlineData(2_000, 1_440, 50, 10)]
    public async Task GetOrCreateAsync_CleansPersistedLegacyLimitsBeforeReturningSettingsAndRuntimeOptions(
        int persistedInterval,
        int expectedInterval,
        int persistedRetention,
        int expectedRetention)
    {
        await using var db = CreateDb();
        var options = new AutoUpdateOptions();
        var service = CreateService(db, options);
        db.UpdateSettings.Add(new UpdateSettings
        {
            Id = 1,
            CheckIntervalMinutes = persistedInterval,
            UpdateBackupPath = "LegacyBackups",
            RetainedUpdateBackupCount = persistedRetention,
            CreateBackupBeforeInstallation = true,
            CancelInstallationOnBackupFailure = false
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var settings = await service.GetOrCreateAsync(TestContext.Current.CancellationToken);
        await service.ApplyToRuntimeOptionsAsync(TestContext.Current.CancellationToken);
        var backupOptions = await service.GetBackupOptionsAsync(TestContext.Current.CancellationToken);
        var persisted = await db.UpdateSettings.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedInterval, settings.CheckIntervalMinutes);
        Assert.Equal(expectedInterval, persisted.CheckIntervalMinutes);
        Assert.Equal(expectedInterval, options.SourceCheck.Interval);
        Assert.Equal(expectedRetention, settings.RetainedUpdateBackupCount);
        Assert.Equal(expectedRetention, persisted.RetainedUpdateBackupCount);
        Assert.Equal(expectedRetention, backupOptions.RetainedBackupCount);
        Assert.Equal("LegacyBackups", backupOptions.Path);
    }

    [Fact]
    public async Task AdminSnapshot_UsesCleanedPersistedLegacyLimits()
    {
        await using var db = CreateDb();
        db.UpdateSettings.Add(new UpdateSettings
        {
            Id = 1,
            CheckIntervalMinutes = 2_000,
            UpdateBackupPath = "LegacyBackups",
            RetainedUpdateBackupCount = 50,
            CreateBackupBeforeInstallation = true,
            CancelInstallationOnBackupFailure = true
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var settingsService = CreateService(db, new AutoUpdateOptions());
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator
            .Setup(x => x.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateStatusSnapshot(
                AutoUpdateState.Idle,
                InstalledVersion: "1.0.0",
                AvailableVersion: null,
                LastCheckedAt: null,
                LastCheckResult: null!,
                LastDownloadResult: null!,
                LastInstallResult: null!,
                LastError: null,
                LastErrorCode: null,
                IsLocked: false,
                LockCreatedAt: null));
        var adminService = new UpdateAdminService(
            orchestrator.Object,
            Mock.Of<IAutoUpdateCommandHandler>(),
            settingsService,
            NullLogger<UpdateAdminService>.Instance);

        var snapshot = await adminService.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var backupOptions = await settingsService.GetBackupOptionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1_440, snapshot.Settings.CheckIntervalMinutes);
        Assert.Equal(10, snapshot.Settings.RetainedUpdateBackupCount);
        Assert.Equal(10, backupOptions.RetainedBackupCount);
    }

    [Fact]
    public void GetDefaultSettings_UsesSameConfiguredFallbacksAsNewSettings()
    {
        using var db = CreateDb();
        var service = CreateService(db, new AutoUpdateOptions(), new Dictionary<string, string?>
        {
            ["AutoUpdate:SourceCheck:Interval"] = "0",
            ["AutoUpdate:Backup:RetainedBackupCount"] = "50"
        });

        var defaults = service.GetDefaultSettings();

        Assert.Equal(1, defaults.CheckIntervalMinutes);
        Assert.Equal(10, defaults.RetainedUpdateBackupCount);
        Assert.Equal("Backups", defaults.UpdateBackupPath);
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
