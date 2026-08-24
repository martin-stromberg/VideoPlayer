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
        var dataSource = new NoopBackupDataSource();
        var history = new BackupOperationHistoryService(db);
        var runner = new VideoWebPlayerAutomaticBackupRunner(backupService, dataSource, history);

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
        private string? _lastBackupName;

        public bool RetentionApplied { get; private set; }

        public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken)
        {
            var descriptor = new BackupDescriptor(
                Path.GetFileName(_lastBackupName ?? "son-20260101-000000.bak"),
                _lastBackupName ?? "son-20260101-000000.bak",
                1,
                DateTimeOffset.UtcNow,
                BackupGeneration.Son,
                "msTools.Backup.Object",
                2,
                true,
                Array.Empty<string>());
            return Task.FromResult<IReadOnlyList<BackupDescriptor>>(new[] { descriptor });
        }

        public Task<Stream> OpenBackupReadAsync(string fileName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupOperationResult> DeleteBackupAsync(string fileName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupResult> StoreAsync(string backupName, BackupGeneration generation, IEnumerable<IBackupData> items, CancellationToken cancellationToken = default)
        {
            _lastBackupName = backupName;
            return Task.FromResult(new BackupResult(backupName, true, "Backup wurde gespeichert."));
        }

        public Task<IReadOnlyList<IBackupData>> RestoreAsync(string backupName, IBackupDataFactory factory, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ApplyRetentionAsync(CancellationToken cancellationToken)
        {
            RetentionApplied = true;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopBackupDataSource : IBackupDataSource
    {
        public Task<IReadOnlyList<IBackupData>> GetBackupDataAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IBackupData>>(Array.Empty<IBackupData>());
    }
}
