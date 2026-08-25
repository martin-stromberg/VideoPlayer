using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using msTools.Backup;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Services.Updates;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class VideoWebPlayerUpdateBackupServiceTests
{
    [Fact]
    public async Task CreateBackupAsync_UsesProgramUpdateGenerationAndReturnsDescriptorPath()
    {
        BackupGeneration? generation = null;
        var descriptor = new BackupDescriptor(
            "programupdate-20260101-000000.bak",
            "Data/Backups/programupdate-20260101-000000.bak",
            123,
            DateTimeOffset.UtcNow,
            BackupGeneration.ProgramUpdate,
            "msTools.Backup.Object",
            2,
            true,
            Array.Empty<string>());

        var backupService = new Mock<IBackupService>();
        backupService
            .Setup(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<BackupGeneration>(), It.IsAny<IEnumerable<IBackupData>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, BackupGeneration g, IEnumerable<IBackupData> _, CancellationToken _) =>
            {
                generation = g;
                return Task.FromResult(new BackupResult(descriptor.Path, true, "Backup wurde gespeichert."));
            });
        backupService
            .Setup(x => x.ListBackupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { descriptor });

        await using var db = CreateDb();
        var service = CreateService(db, backupService.Object);

        var result = await service.CreateBackupAsync(new UpdateBackupRequest("ignored", "test"), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(descriptor.Path, result.BackupFilePath);
        Assert.Equal(BackupGeneration.ProgramUpdate, generation);
        backupService.Verify(x => x.ApplyRetentionAsync(It.IsAny<CancellationToken>()), Times.Once);

        var history = await db.BackupOperationHistories.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ProgramUpdateBackup", history.Operation);
        Assert.Equal("ProgramUpdate", history.Generation);
        Assert.True(history.Succeeded);
    }

    [Fact]
    public async Task CreateBackupAsync_WhenBackupFails_ReturnsFailureAndWritesHistory()
    {
        var backupService = new Mock<IBackupService>();
        backupService
            .Setup(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<BackupGeneration>(), It.IsAny<IEnumerable<IBackupData>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupResult(string.Empty, false, "kaputt"));

        await using var db = CreateDb();
        var service = CreateService(db, backupService.Object);

        var result = await service.CreateBackupAsync(new UpdateBackupRequest("ignored", "test"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        backupService.Verify(x => x.ApplyRetentionAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.False((await db.BackupOperationHistories.SingleAsync(TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task CreateBackupAsync_UsesBackupInfrastructureRetentionInsteadOfTargetDirectory()
    {
        var targetDirectory = Path.Combine(Path.GetTempPath(), $"vwp-update-target-{Guid.NewGuid():N}");
        Directory.CreateDirectory(targetDirectory);
        var sentinel = Path.Combine(targetDirectory, "manual-backup.zip");
        File.WriteAllText(sentinel, "keep");

        try
        {
            var descriptor = new BackupDescriptor(
                "programupdate-20260101-000000.bak",
                "Data/Backups/programupdate-20260101-000000.bak",
                123,
                DateTimeOffset.UtcNow,
                BackupGeneration.ProgramUpdate,
                "msTools.Backup.Object",
                2,
                true,
                Array.Empty<string>());

            var backupService = new Mock<IBackupService>();
            backupService
                .Setup(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<BackupGeneration>(), It.IsAny<IEnumerable<IBackupData>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BackupResult(descriptor.Path, true, "Backup wurde gespeichert."));
            backupService
                .Setup(x => x.ListBackupsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { descriptor });

            await using var db = CreateDb();
            var service = CreateService(db, backupService.Object);

            var result = await service.CreateBackupAsync(new UpdateBackupRequest(targetDirectory, "test"), TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.True(File.Exists(sentinel));
            backupService.Verify(x => x.ApplyRetentionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(targetDirectory))
                Directory.Delete(targetDirectory, recursive: true);
        }
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"update-backup-service-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, new EventManager());
    }

    private static VideoWebPlayerUpdateBackupService CreateService(ApplicationDbContext db, IBackupService backupService)
        => new(
            backupService,
            new NoopBackupDataSource(),
            new BackupOperationHistoryService(db),
            NullLogger<VideoWebPlayerUpdateBackupService>.Instance);

    private sealed class NoopBackupDataSource : IBackupDataSource
    {
        public Task<IReadOnlyList<IBackupData>> GetBackupDataAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IBackupData>>(Array.Empty<IBackupData>());
    }
}
