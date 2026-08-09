using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class BackupSettingsServiceTests
{
    [Fact]
    public async Task GetOptionsAsync_UsesConfiguredDefaultsAndPersistsUpdates()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"backup-settings-{Guid.NewGuid():N}")
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backups:Path"] = "Data/TestBackups",
                ["Backups:AutomaticBackupsEnabled"] = "true",
                ["Backups:MaxUploadSizeBytes"] = "2097152",
                ["Backups:Retention:SonCount"] = "3",
                ["Backups:Retention:FatherCount"] = "2",
                ["Backups:Retention:GrandfatherCount"] = "1"
            })
            .Build();

        await using var db = new ApplicationDbContext(options, new EventManager());
        var service = new BackupSettingsService(db, configuration);
        var backupOptions = await service.GetOptionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Data/TestBackups", backupOptions.StoragePath);
        Assert.True(backupOptions.AutomaticBackupsEnabled);
        Assert.Equal(3, backupOptions.Retention.SonCount);

        await service.UpdateAsync(new BackupSettings
        {
            StoragePath = "Data/Updated",
            AutomaticBackupsEnabled = false,
            SonRetentionCount = 9,
            FatherRetentionCount = 8,
            GrandfatherRetentionCount = 7,
            MaxUploadSizeBytes = 4 * 1024 * 1024
        }, TestContext.Current.CancellationToken);

        backupOptions = await service.GetOptionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Data/Updated", backupOptions.StoragePath);
        Assert.False(backupOptions.AutomaticBackupsEnabled);
        Assert.Equal(9, backupOptions.Retention.SonCount);
        Assert.Equal(8, backupOptions.Retention.FatherCount);
        Assert.Equal(7, backupOptions.Retention.GrandfatherCount);
        Assert.Equal(4 * 1024 * 1024, backupOptions.MaxUploadSizeBytes);
    }

    [Fact]
    public async Task GetOptionsAsync_RaisesPersistedLegacyUploadLimitToConfiguredDefault()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"backup-settings-{Guid.NewGuid():N}")
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backups:MaxUploadSizeBytes"] = "5368709120"
            })
            .Build();

        await using var db = new ApplicationDbContext(options, new EventManager());
        db.BackupSettings.Add(new BackupSettings
        {
            StoragePath = "Data/Backups",
            MaxUploadSizeBytes = 512L * 1024L * 1024L
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new BackupSettingsService(db, configuration);
        var backupOptions = await service.GetOptionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5368709120, backupOptions.MaxUploadSizeBytes);
    }
}
