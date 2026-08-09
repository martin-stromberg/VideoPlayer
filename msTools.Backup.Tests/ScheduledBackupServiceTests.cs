using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using msTools.Backup;
using Xunit;

namespace msTools.Backup.Tests;

public sealed class ScheduledBackupServiceTests
{
    [Fact]
    public async Task RunOnceAsync_UsesTimeProviderToCreateDueAutomaticBackup()
    {
        var now = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        var backupService = new RecordingBackupService(Array.Empty<BackupDescriptor>());
        var runner = new RecordingAutomaticBackupRunner();
        var services = new ServiceCollection()
            .AddScoped<IBackupOptionsProvider>(_ => new StaticOptionsProvider(new BackupOptions
            {
                AutomaticBackupsEnabled = true,
                Schedule = new BackupScheduleOptions
                {
                    Enabled = true,
                    CheckInterval = TimeSpan.FromMinutes(15),
                    SonFrequency = TimeSpan.FromDays(1),
                    FatherFrequency = TimeSpan.Zero,
                    GrandfatherFrequency = TimeSpan.Zero
                }
            }))
            .AddScoped<IBackupService>(_ => backupService)
            .AddScoped<IAutomaticBackupRunner>(_ => runner)
            .BuildServiceProvider();

        var service = new ScheduledBackupService(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(now),
            NullLogger<ScheduledBackupService>.Instance);

        var delay = await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(15), delay);
        Assert.Equal(BackupGeneration.Son, runner.RequestedGeneration);
    }

    private sealed class StaticOptionsProvider : IBackupOptionsProvider
    {
        private readonly BackupOptions _options;

        public StaticOptionsProvider(BackupOptions options)
        {
            _options = options;
        }

        public Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(_options);
    }

    private sealed class RecordingAutomaticBackupRunner : IAutomaticBackupRunner
    {
        public BackupGeneration? RequestedGeneration { get; private set; }

        public Task<BackupOperationResult> RunAutomaticBackupAsync(BackupGeneration generation, CancellationToken cancellationToken)
        {
            RequestedGeneration = generation;
            return Task.FromResult(BackupOperationResult.Success("ok"));
        }
    }

    private sealed class RecordingBackupService : IBackupService
    {
        private readonly IReadOnlyList<BackupDescriptor> _descriptors;

        public RecordingBackupService(IReadOnlyList<BackupDescriptor> descriptors)
        {
            _descriptors = descriptors;
        }

        public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken)
            => Task.FromResult(_descriptors);

        public Task<BackupOperationResult> CreateBackupAsync(BackupCreateRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

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

        public Task ApplyRetentionAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
