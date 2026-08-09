using Microsoft.EntityFrameworkCore;
using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Provides persisted backup settings and maps them to library options.
/// </summary>
public sealed class BackupSettingsService : IBackupOptionsProvider
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Creates a new settings service.
    /// </summary>
    public BackupSettingsService(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the persisted settings row, creating it with configured defaults when missing.
    /// </summary>
    public async Task<BackupSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.BackupSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
            return settings;

        settings = new BackupSettings
        {
            StoragePath = _configuration["Backups:Path"] ?? Path.Combine("Data", "Backups"),
            AutomaticBackupsEnabled = _configuration.GetValue("Backups:AutomaticBackupsEnabled", false),
            MaxUploadSizeBytes = _configuration.GetValue("Backups:MaxUploadSizeBytes", 512L * 1024L * 1024L),
            SonRetentionCount = _configuration.GetValue("Backups:Retention:SonCount", 7),
            FatherRetentionCount = _configuration.GetValue("Backups:Retention:FatherCount", 4),
            GrandfatherRetentionCount = _configuration.GetValue("Backups:Retention:GrandfatherCount", 12),
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.BackupSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    /// <summary>
    /// Updates persisted backup settings.
    /// </summary>
    public async Task UpdateAsync(BackupSettings updated, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        settings.StoragePath = string.IsNullOrWhiteSpace(updated.StoragePath)
            ? Path.Combine("Data", "Backups")
            : updated.StoragePath.Trim();
        settings.AutomaticBackupsEnabled = updated.AutomaticBackupsEnabled;
        settings.SonRetentionCount = Math.Max(0, updated.SonRetentionCount);
        settings.FatherRetentionCount = Math.Max(0, updated.FatherRetentionCount);
        settings.GrandfatherRetentionCount = Math.Max(0, updated.GrandfatherRetentionCount);
        settings.MaxUploadSizeBytes = Math.Max(1024 * 1024, updated.MaxUploadSizeBytes);
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        return new BackupOptions
        {
            StoragePath = settings.StoragePath,
            MaxUploadSizeBytes = settings.MaxUploadSizeBytes,
            AutomaticBackupsEnabled = settings.AutomaticBackupsEnabled,
            Schedule = new BackupScheduleOptions
            {
                Enabled = settings.AutomaticBackupsEnabled,
                CheckInterval = TimeSpan.FromHours(1),
                SonFrequency = TimeSpan.FromDays(1),
                FatherFrequency = TimeSpan.FromDays(7),
                GrandfatherFrequency = TimeSpan.FromDays(30)
            },
            Retention = new BackupRetentionOptions
            {
                SonCount = settings.SonRetentionCount,
                FatherCount = settings.FatherRetentionCount,
                GrandfatherCount = settings.GrandfatherRetentionCount
            }
        };
    }
}
