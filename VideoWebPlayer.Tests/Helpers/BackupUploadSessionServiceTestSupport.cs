using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using msTools.Backup;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Tests.Helpers;

internal static class BackupUploadSessionServiceTestSupport
{
    public static BackupUploadSessionService CreateService(
        ServiceProvider provider,
        string tempDirectory,
        TimeProvider? timeProvider = null)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider ?? TimeProvider.System,
            NullLogger<BackupUploadSessionService>.Instance,
            tempDirectory);

    public static ServiceProvider CreateProvider(long maxUploadSizeBytes = 5L * 1024 * 1024 * 1024)
    {
        var services = new ServiceCollection();
        services.AddScoped<IBackupOptionsProvider>(_ => new FakeBackupOptionsProvider(maxUploadSizeBytes));
        return services.BuildServiceProvider();
    }

    public static async Task DisposeSessionAsync(BackupUploadSession? session)
    {
        if (session is null)
            return;

        await session.DisposeAsync();
        if (File.Exists(session.TempPath))
            File.Delete(session.TempPath);
    }

    /// <summary>
    /// Isolated temporary directory for a single service instance under test, so that
    /// orphan-cleanup scans never touch files of parallel test classes. Deleted on dispose.
    /// </summary>
    public sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vwp-backup-upload-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Temp directory cleanup is best-effort.
            }
        }
    }

    private sealed class FakeBackupOptionsProvider : IBackupOptionsProvider
    {
        private readonly long _maxUploadSizeBytes;

        public FakeBackupOptionsProvider(long maxUploadSizeBytes)
        {
            _maxUploadSizeBytes = maxUploadSizeBytes;
        }

        public Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new BackupOptions
            {
                StoragePath = Path.Combine("Data", "Backups"),
                MaxUploadSizeBytes = _maxUploadSizeBytes
            });
    }
}
