using Microsoft.EntityFrameworkCore;
using msTools.Backup;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class VideoWebPlayerAutomaticBackupRunnerTests
{
    [Fact]
    public async Task RunAutomaticBackupAsync_RecordsHistoryAndAppliesRetention()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"automatic-backup-history-{Guid.NewGuid():N}")
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        var backupService = new RecordingBackupService();
        var provider = new NoopBackupDataProvider();
        var history = new BackupOperationHistoryService(db);
        var runner = new VideoWebPlayerAutomaticBackupRunner(backupService, provider, history);

        var result = await runner.RunAutomaticBackupAsync(BackupGeneration.Son, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(backupService.RetentionApplied);

        var row = await db.BackupOperationHistories.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("AutomaticBackup", row.Operation);
        Assert.Equal(BackupGeneration.Son.ToString(), row.Generation);
        Assert.True(row.Succeeded);
        Assert.Null(row.UserId);
    }

    private sealed class RecordingBackupService : IBackupService
    {
        public bool RetentionApplied { get; private set; }

        public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupOperationResult> CreateBackupAsync(BackupCreateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(BackupOperationResult.Success(
                "Backup wurde erstellt.",
                new BackupDescriptor(
                    "scheduled.zip",
                    "scheduled.zip",
                    1,
                    DateTimeOffset.UtcNow,
                    request.Generation,
                    "Provider",
                    1,
                    true,
                    Array.Empty<string>())));

        public Task<BackupValidationResult> ValidateUploadAsync(Stream source, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupOperationResult> ImportUploadedBackupAsync(Stream source, string originalFileName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Stream> OpenBackupReadAsync(string fileName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupOperationResult> DeleteBackupAsync(string fileName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupOperationResult> RestoreBackupAsync(BackupRestoreRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupResult> StoreAsync(string backupName, IEnumerable<IBackupData> items, CancellationToken cancellationToken)
            => Task.FromResult(new BackupResult(backupName, true, "Backup wurde gespeichert."));

        public Task<IReadOnlyList<IBackupData>> RestoreAsync(string backupName, IBackupDataFactory factory, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task ApplyRetentionAsync(CancellationToken cancellationToken)
        {
            RetentionApplied = true;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopBackupDataProvider : IBackupDataProvider
    {
        public string ProviderId => "Test";

        public Task ExportAsync(Stream target, BackupExportContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<BackupValidationResult> ValidateAsync(Stream source, BackupValidationContext context, CancellationToken cancellationToken)
            => Task.FromResult(BackupValidationResult.Valid);

        public Task RestoreAsync(Stream source, BackupRestoreContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
