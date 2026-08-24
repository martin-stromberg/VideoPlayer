using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        var started = service.StartRestore("backup.bak", "admin", confirmRestore: true);

        Assert.True(started.Started);
        var running = await WaitForSnapshotAsync(service, x => x.Status == RestoreBackupJobStatus.Running);
        Assert.True(running.IsActive);

        var parallel = service.StartRestore("other.bak", "admin", confirmRestore: true);
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
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IBackupDataSource, NoopBackupDataSource>();
        services.AddScoped<VideoWebPlayerBackupDataFactory>();
        services.AddScoped<IBackupOptionsProvider, NoopBackupOptionsProvider>();
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

        public Task<Stream> OpenBackupReadAsync(string fileName, CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task<BackupOperationResult> DeleteBackupAsync(string fileName, CancellationToken cancellationToken)
            => Task.FromResult(BackupOperationResult.Success("Backup wurde gelöscht."));

        public Task<BackupResult> StoreAsync(string backupName, BackupGeneration generation, IEnumerable<IBackupData> items, CancellationToken cancellationToken = default)
            => Task.FromResult(new BackupResult(backupName, true, "Backup wurde gespeichert."));

        public async Task<IReadOnlyList<IBackupData>> RestoreAsync(string backupName, IBackupDataFactory factory, CancellationToken cancellationToken = default)
        {
            if (factory is VideoWebPlayerBackupDataFactory f)
            {
                f.Progress?.Report(new BackupRestoreProgress(
                    "AspNetUsers",
                    1,
                    2,
                    3,
                    5,
                    "Datensatz wurde wiederhergestellt."));
            }

            await _release.Task.WaitAsync(cancellationToken);
            return Array.Empty<IBackupData>();
        }

        public Task ApplyRetentionAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NoopBackupDataSource : IBackupDataSource
    {
        public Task<IReadOnlyList<IBackupData>> GetBackupDataAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IBackupData>>(Array.Empty<IBackupData>());
    }

    private sealed class NoopBackupOptionsProvider : IBackupOptionsProvider
    {
        public Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new BackupOptions { StoragePath = Path.Combine("Data", "Backups") });
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "VideoWebPlayer";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
