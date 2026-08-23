using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using msTools.Backup;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class RestoreBackupJobServiceTests
{
    [Fact]
    public async Task StartRestore_RunsInBackgroundAndRejectsParallelRestore()
    {
        var backupService = new BlockingRestoreBackupService();
        using var provider = CreateProvider(backupService);
        var service = new RestoreBackupJobService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RestoreBackupJobService>.Instance);

        var started = service.StartRestore("backup.zip", "admin", confirmRestore: true);

        Assert.True(started.Started);
        var running = await WaitForSnapshotAsync(service, x => x.Status == RestoreBackupJobStatus.Running);
        Assert.True(running.IsActive);

        var parallel = service.StartRestore("other.zip", "admin", confirmRestore: true);
        Assert.False(parallel.Started);
        Assert.Equal(started.Snapshot.Id, parallel.Snapshot.Id);

        backupService.Complete();
        var completed = await WaitForSnapshotAsync(service, x => x.Status == RestoreBackupJobStatus.Succeeded);

        Assert.False(completed.IsActive);
        Assert.Equal("Backup wurde wiederhergestellt.", completed.Message);
        Assert.Equal("AspNetUsers", completed.Progress.DataSetName);
        Assert.Equal(1, completed.Progress.DataSetNumber);
        Assert.Equal(2, completed.Progress.DataSetTotal);
        Assert.Equal(3, completed.Progress.RecordNumber);
        Assert.Equal(5, completed.Progress.RecordTotal);
    }

    private static ServiceProvider CreateProvider(IBackupService backupService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new EventManager());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IBackupDataProvider, NoopBackupDataProvider>();
        services.AddScoped<VideoWebPlayerBackupDataFactory>();
        services.AddScoped<BackupSettingsService>();
        services.AddScoped<BackupOperationHistoryService>();
        services.AddScoped<VideoWebPlayerBackupFacade>();
        services.AddSingleton(backupService);
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static async Task<RestoreBackupJobSnapshot> WaitForSnapshotAsync(
        RestoreBackupJobService service,
        Func<RestoreBackupJobSnapshot, bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var snapshot = service.GetSnapshot();
            if (predicate(snapshot))
                return snapshot;

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Restore job did not reach the expected state.");
    }

    private sealed class BlockingRestoreBackupService : IBackupService
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete() => _release.TrySetResult();

        public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BackupDescriptor>>(Array.Empty<BackupDescriptor>());

        public Task<BackupOperationResult> CreateBackupAsync(BackupCreateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(BackupOperationResult.Success("Backup wurde erstellt."));

        public Task<BackupValidationResult> ValidateUploadAsync(Stream source, CancellationToken cancellationToken)
            => Task.FromResult(BackupValidationResult.Valid);

        public Task<BackupOperationResult> ImportUploadedBackupAsync(Stream source, string originalFileName, CancellationToken cancellationToken)
            => Task.FromResult(BackupOperationResult.Success("Backup wurde hochgeladen."));

        public Task<Stream> OpenBackupReadAsync(string fileName, CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task<BackupOperationResult> DeleteBackupAsync(string fileName, CancellationToken cancellationToken)
            => Task.FromResult(BackupOperationResult.Success("Backup wurde gelöscht."));

        public async Task<BackupOperationResult> RestoreBackupAsync(BackupRestoreRequest request, CancellationToken cancellationToken)
        {
            request.Progress?.Report(new BackupRestoreProgress("AspNetUsers", 1, 2, 3, 5, "Datensatz wurde wiederhergestellt."));
            await _release.Task.WaitAsync(cancellationToken);
            return BackupOperationResult.Success("Backup wurde wiederhergestellt.");
        }

        public Task<BackupResult> StoreAsync(string backupName, IEnumerable<IBackupData> items, CancellationToken cancellationToken)
            => Task.FromResult(new BackupResult(backupName, true, "Backup wurde gespeichert."));

        public async Task<IReadOnlyList<IBackupData>> RestoreAsync(string backupName, IBackupDataFactory factory, CancellationToken cancellationToken)
        {
            if (factory is VideoWebPlayerBackupDataFactory f)
            {
                f.Progress?.Report(new BackupRestoreProgress("AspNetUsers", 1, 2, 3, 5, "Datensatz wurde wiederhergestellt."));
            }

            await _release.Task.WaitAsync(cancellationToken);
            return Array.Empty<IBackupData>();
        }

        public Task ApplyRetentionAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
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
