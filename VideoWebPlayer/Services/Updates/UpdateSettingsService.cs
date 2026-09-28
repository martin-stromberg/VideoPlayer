using Microsoft.EntityFrameworkCore;
using msTools.Updater;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Provides persisted update settings and maps them to runtime updater options.
/// </summary>
public interface IUpdateSettingsService
{
    /// <summary>
    /// Gets the default settings that would be used for a new settings row.
    /// </summary>
    /// <returns>The default settings.</returns>
    UpdateSettings GetDefaultSettings();

    /// <summary>
    /// Gets the default settings that would be used for a new settings row.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted settings.</returns>
    Task<UpdateSettings> GetOrCreateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates persisted settings and applies them to runtime options.
    /// </summary>
    /// <param name="update">The new settings values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted settings.</returns>
    Task<UpdateSettings> UpdateAsync(UpdateSettingsUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies persisted settings to runtime updater options.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyToRuntimeOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current update backup options from persisted settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update backup options.</returns>
    Task<UpdateBackupOptions> GetBackupOptionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists update settings and applies them to the runtime-mutable updater options.
/// </summary>
public sealed class UpdateSettingsService : IUpdateSettingsService
{
    private const int SettingsRowId = 1;
    private const int DefaultCheckIntervalMinutes = 360;
    private const string DefaultBackupPath = "Backups";
    private static readonly TimeOnly ClosedWindow = TimeOnly.MinValue;

    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly AutoUpdateOptions _autoUpdateOptions;
    private readonly VideoWebPlayerUpdateSourceFactory _sourceFactory;

    /// <summary>
    /// Creates a new update settings service.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="configuration">The application configuration providing default values.</param>
    /// <param name="autoUpdateOptions">The runtime-mutable updater options.</param>
    /// <param name="sourceFactory">The factory creating the update source.</param>
    public UpdateSettingsService(
        ApplicationDbContext db,
        IConfiguration configuration,
        AutoUpdateOptions autoUpdateOptions,
        VideoWebPlayerUpdateSourceFactory sourceFactory)
    {
        _db = db;
        _configuration = configuration;
        _autoUpdateOptions = autoUpdateOptions;
        _sourceFactory = sourceFactory;
    }

    /// <summary>
    /// Gets the singleton settings row, creating it from configuration when missing.
    /// </summary>
    /// <returns>The default settings.</returns>
    public UpdateSettings GetDefaultSettings()
        => CreateDefaults();

    /// <summary>
    /// Gets the singleton settings row, creating it from configuration when missing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted settings.</returns>
    public async Task<UpdateSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.UpdateSettings.FirstOrDefaultAsync(x => x.Id == SettingsRowId, cancellationToken);
        if (settings is not null)
        {
            if (NormalizePersistedSettings(settings))
                await _db.SaveChangesAsync(cancellationToken);

            return settings;
        }

        settings = CreateDefaults();
        _db.UpdateSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    /// <summary>
    /// Updates persisted settings and applies them to the updater runtime options.
    /// </summary>
    /// <param name="update">The new settings values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted settings.</returns>
    public async Task<UpdateSettings> UpdateAsync(UpdateSettingsUpdate update, CancellationToken cancellationToken = default)
    {
        ValidateUpdate(update);

        var settings = await GetOrCreateAsync(cancellationToken);
        settings.AutomaticChecksEnabled = update.AutomaticChecksEnabled;
        settings.CheckIntervalMinutes = update.CheckIntervalMinutes;
        settings.AllowPrereleaseUpdates = update.AllowPrereleaseUpdates;
        settings.AutomaticInstallationEnabled = update.AutomaticInstallationEnabled;
        settings.AutomaticDownloadEnabled = update.AutomaticInstallationEnabled || update.AutomaticDownloadEnabled;
        settings.ServiceName = NormalizeOptional(update.ServiceName, 200);
        settings.CreateBackupBeforeInstallation = update.CreateBackupBeforeInstallation;
        settings.CancelInstallationOnBackupFailure = update.CancelInstallationOnBackupFailure;
        settings.UpdateBackupPath = string.IsNullOrWhiteSpace(update.UpdateBackupPath)
            ? DefaultBackupPath
            : update.UpdateBackupPath.Trim();
        settings.RetainedUpdateBackupCount = update.RetainedUpdateBackupCount;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        ApplyToRuntimeOptions(settings);
        return settings;
    }

    /// <summary>
    /// Applies persisted settings to runtime updater options.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ApplyToRuntimeOptionsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        ApplyToRuntimeOptions(settings);
    }

    /// <summary>
    /// Gets current update backup options from persisted settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update backup options.</returns>
    public async Task<UpdateBackupOptions> GetBackupOptionsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        return new UpdateBackupOptions
        {
            Enabled = settings.CreateBackupBeforeInstallation,
            Path = settings.UpdateBackupPath,
            RetainedBackupCount = settings.RetainedUpdateBackupCount,
            CancelInstallationOnFailure = settings.CancelInstallationOnBackupFailure
        };
    }

    private void ApplyToRuntimeOptions(UpdateSettings settings)
    {
        _autoUpdateOptions.Enabled = true;
        _autoUpdateOptions.SourceCheck ??= new SourceCheckOptions();
        _autoUpdateOptions.SourceCheck.Interval = ClampCheckInterval(settings.CheckIntervalMinutes);
        _autoUpdateOptions.SourceCheck.TimeRanges = settings.AutomaticChecksEnabled
            ? []
            : CreateDisabledSourceCheckWindows();
        _autoUpdateOptions.AllowPrereleaseUpdates = settings.AllowPrereleaseUpdates;
        _autoUpdateOptions.EnableAutomaticInstallation = settings.AutomaticInstallationEnabled;
        _autoUpdateOptions.EnableAutomaticDownload = settings.AutomaticInstallationEnabled || settings.AutomaticDownloadEnabled;
        _autoUpdateOptions.ServiceName = settings.ServiceName;

        _autoUpdateOptions.Source = _sourceFactory.Create(settings.AllowPrereleaseUpdates);
    }

    private UpdateSettings CreateDefaults()
        => new()
        {
            Id = SettingsRowId,
            AutomaticChecksEnabled = _configuration.GetValue("AutoUpdate:Enabled", true),
            CheckIntervalMinutes = ClampCheckInterval(_configuration.GetValue("AutoUpdate:SourceCheck:Interval", DefaultCheckIntervalMinutes)),
            AllowPrereleaseUpdates = _configuration.GetValue("AutoUpdate:AllowPrereleaseUpdates", false),
            AutomaticInstallationEnabled = _configuration.GetValue("AutoUpdate:EnableAutomaticInstallation", false),
            AutomaticDownloadEnabled = _configuration.GetValue("AutoUpdate:EnableAutomaticDownload", true),
            ServiceName = NormalizeOptional(_configuration["AutoUpdate:ServiceName"], 200),
            CreateBackupBeforeInstallation = _configuration.GetValue("AutoUpdate:Backup:Enabled", true),
            CancelInstallationOnBackupFailure = _configuration.GetValue("AutoUpdate:Backup:CancelInstallationOnFailure", true),
            UpdateBackupPath = _configuration["AutoUpdate:Backup:Path"] ?? DefaultBackupPath,
            RetainedUpdateBackupCount = ClampRetainedBackups(_configuration.GetValue("AutoUpdate:Backup:RetainedBackupCount", 5)),
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static int ClampCheckInterval(int value)
        => Math.Clamp(value, 1, 24 * 60);

    private static int ClampRetainedBackups(int value)
        => Math.Clamp(value, 1, 10);

    private static void ValidateUpdate(UpdateSettingsUpdate update)
    {
        if (update.CheckIntervalMinutes is < 1 or > 24 * 60)
            throw new ArgumentOutOfRangeException(
                nameof(update.CheckIntervalMinutes),
                "Das Prüfintervall muss zwischen 1 und 1440 Minuten liegen.");

        if (update.RetainedUpdateBackupCount is < 1 or > 10)
            throw new ArgumentOutOfRangeException(
                nameof(update.RetainedUpdateBackupCount),
                "Es können 1 bis 10 Update-Backups aufbewahrt werden.");
    }

    private static bool NormalizePersistedSettings(UpdateSettings settings)
    {
        var checkInterval = ClampCheckInterval(settings.CheckIntervalMinutes);
        var retainedBackups = ClampRetainedBackups(settings.RetainedUpdateBackupCount);
        if (settings.CheckIntervalMinutes == checkInterval &&
            settings.RetainedUpdateBackupCount == retainedBackups)
            return false;

        settings.CheckIntervalMinutes = checkInterval;
        settings.RetainedUpdateBackupCount = retainedBackups;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static List<SourceCheckTimeRange> CreateDisabledSourceCheckWindows()
        => Enum.GetValues<DayOfWeek>()
            .Select(day => new SourceCheckTimeRange
            {
                DayOfWeek = day,
                StartTime = ClosedWindow,
                EndTime = ClosedWindow
            })
            .ToList();
}

/// <summary>
/// Describes administrator-submitted update settings.
/// </summary>
/// <param name="AutomaticChecksEnabled">Whether automatic update checks are enabled.</param>
/// <param name="CheckIntervalMinutes">The interval between automatic checks in minutes.</param>
/// <param name="AllowPrereleaseUpdates">Whether prerelease versions are accepted.</param>
/// <param name="AutomaticInstallationEnabled">Whether found updates are installed automatically.</param>
/// <param name="AutomaticDownloadEnabled">Whether found updates are downloaded automatically.</param>
/// <param name="ServiceName">The name of the service to restart after an installation.</param>
/// <param name="CreateBackupBeforeInstallation">Whether a backup is created before an installation.</param>
/// <param name="CancelInstallationOnBackupFailure">Whether a failed backup cancels the installation.</param>
/// <param name="UpdateBackupPath">The directory update backups are stored in.</param>
/// <param name="RetainedUpdateBackupCount">The number of update backups to keep.</param>
/// <returns>The settings value.</returns>
public sealed record UpdateSettingsUpdate(
    bool AutomaticChecksEnabled,
    int CheckIntervalMinutes,
    bool AllowPrereleaseUpdates,
    bool AutomaticInstallationEnabled,
    bool AutomaticDownloadEnabled,
    string? ServiceName,
    bool CreateBackupBeforeInstallation,
    bool CancelInstallationOnBackupFailure,
    string? UpdateBackupPath,
    int RetainedUpdateBackupCount);
