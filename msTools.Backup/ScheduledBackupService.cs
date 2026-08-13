using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace msTools.Backup;

/// <summary>
/// Creates due automatic backups and applies retention.
/// </summary>
public sealed class ScheduledBackupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScheduledBackupService> _logger;

    /// <summary>
    /// Creates a new scheduled backup service.
    /// </summary>
    public ScheduledBackupService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<ScheduledBackupService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = await RunOnceAsync(stoppingToken);
            await Task.Delay(delay, _timeProvider, stoppingToken);
        }
    }

    internal async Task<TimeSpan> RunOnceAsync(CancellationToken stoppingToken)
    {
        TimeSpan delay = TimeSpan.FromHours(1);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var optionsProvider = scope.ServiceProvider.GetRequiredService<IBackupOptionsProvider>();
            var options = await optionsProvider.GetOptionsAsync(stoppingToken);
            delay = options.Schedule.CheckInterval <= TimeSpan.Zero ? TimeSpan.FromHours(1) : options.Schedule.CheckInterval;

            if (options.AutomaticBackupsEnabled && options.Schedule.Enabled)
            {
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                var backups = await backupService.ListBackupsAsync(stoppingToken);
                var due = GetDueGeneration(backups, options, _timeProvider.GetUtcNow());
                if (due.HasValue)
                {
                    var runner = scope.ServiceProvider.GetRequiredService<IAutomaticBackupRunner>();
                    var result = await runner.RunAutomaticBackupAsync(due.Value, stoppingToken);
                    if (!result.Succeeded)
                        _logger.LogWarning("Automatic backup failed: {Message}", result.Message);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automatic backup check failed.");
        }

        return delay;
    }

    internal static BackupGeneration? GetDueGeneration(IReadOnlyList<BackupDescriptor> backups, BackupOptions options, DateTimeOffset now)
    {
        if (IsDue(backups, BackupGeneration.Grandfather, options.Schedule.GrandfatherFrequency, now))
            return BackupGeneration.Grandfather;
        if (IsDue(backups, BackupGeneration.Father, options.Schedule.FatherFrequency, now))
            return BackupGeneration.Father;
        if (IsDue(backups, BackupGeneration.Son, options.Schedule.SonFrequency, now))
            return BackupGeneration.Son;

        return null;
    }

    private static bool IsDue(IReadOnlyList<BackupDescriptor> backups, BackupGeneration generation, TimeSpan frequency, DateTimeOffset now)
    {
        if (frequency <= TimeSpan.Zero)
            return false;

        var latest = backups
            .Where(x => x.IsValid && x.Generation == generation)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        return latest is null || now - latest.CreatedAtUtc >= frequency;
    }
}
