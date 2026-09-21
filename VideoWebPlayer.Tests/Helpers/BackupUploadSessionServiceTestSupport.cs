using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using msTools.Backup;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Tests.Helpers;

internal static class BackupUploadSessionServiceTestSupport
{
    public static BackupUploadSessionService CreateService(ServiceProvider provider, TimeProvider? timeProvider = null)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider ?? TimeProvider.System,
            NullLogger<BackupUploadSessionService>.Instance);

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
