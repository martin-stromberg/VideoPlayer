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
        BackupCreateRequest? request = null;
        var descriptor = new BackupDescriptor(
            "backup.zip",
            "Data/Backups/backup.zip",
            123,
            DateTimeOffset.UtcNow,
            BackupGeneration.ProgramUpdate,
            "VideoWebPlayer",
            1,
            true,
            Array.Empty<string>());

        var backupService = new Mock<IBackupService>();
        backupService
            .Setup(x => x.CreateBackupAsync(It.IsAny<BackupCreateRequest>(), It.IsAny<CancellationToken>()))
            .Returns((BackupCreateRequest r, CancellationToken _) =>
            {
                request = r;
                return Task.FromResult(BackupOperationResult.Success("Backup wurde erstellt.", descriptor));
            });

        await using var db = CreateDb();
        var service = CreateService(db, backupService.Object);

        var result = await service.CreateBackupAsync(new UpdateBackupRequest("ignored", "test"), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("Data/Backups/backup.zip", result.BackupFilePath);
        Assert.Equal(BackupGeneration.ProgramUpdate, request?.Generation);
        backupService.Verify(x => x.ApplyRetentionAsync(It.IsAny<CancellationToken>()), Times.Once);

        var history = await db.BackupOperationHistories.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ProgramUpdateBackup", history.Operation);
        Assert.True(history.Succeeded);
        Assert.Equal("ProgramUpdate", history.Generation);
    }

    [Fact]
    public async Task CreateBackupAsync_WhenBackupFails_ReturnsFailureAndWritesHistory()
    {
        var backupService = new Mock<IBackupService>();
        backupService
            .Setup(x => x.CreateBackupAsync(It.IsAny<BackupCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BackupOperationResult.Failure("kaputt", "Fehler"));

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
                "program-update.zip",
                "Data/Backups/program-update.zip",
                123,
                DateTimeOffset.UtcNow,
                BackupGeneration.ProgramUpdate,
                "VideoWebPlayer",
                1,
                true,
                Array.Empty<string>());

            var backupService = new Mock<IBackupService>();
            backupService
                .Setup(x => x.CreateBackupAsync(It.IsAny<BackupCreateRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BackupOperationResult.Success("Backup wurde erstellt.", descriptor));

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
            new BackupOperationHistoryService(db),
            NullLogger<VideoWebPlayerUpdateBackupService>.Instance);
}
