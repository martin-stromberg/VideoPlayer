using Microsoft.Extensions.Options;

namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Coordinates the data backup that is created before a program update is installed: resolves the optional
/// <see cref="IUpdateBackupService"/>, provides the configured target directory and enforces the configured
/// number of retained backups.
/// </summary>
public sealed class UpdateBackupCoordinator
{
    private readonly IServiceProvider _services;
    private readonly IOptions<UpdateBackupOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<UpdateBackupCoordinator> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="services">Used to resolve the optional <see cref="IUpdateBackupService"/> in its own scope.</param>
    /// <param name="options">The backup configuration.</param>
    /// <param name="environment">Used to resolve a relative backup path against the content root.</param>
    /// <param name="logger">Logger instance.</param>
    public UpdateBackupCoordinator(
        IServiceProvider services,
        IOptions<UpdateBackupOptions> options,
        IHostEnvironment environment,
        ILogger<UpdateBackupCoordinator> logger)
    {
        _services = services;
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Creates a backup for the given reason.
    /// </summary>
    /// <param name="reason">A short description of what triggered the backup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the update may proceed, i.e. the backup succeeded, backups are disabled or a
    /// failed backup must not cancel the installation.
    /// </returns>
    public async Task<bool> CreateBackupAsync(string reason, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("Update-Backup ist deaktiviert; es wird keine Sicherung erstellt.");
            return true;
        }

        using var scope = _services.CreateScope();
        var backupService = scope.ServiceProvider.GetService<IUpdateBackupService>();
        if (backupService is null)
        {
            _logger.LogWarning(
                "Es ist kein {Service} registriert; vor dem Update wird keine Sicherung erstellt.",
                nameof(IUpdateBackupService));
            return !options.CancelInstallationOnFailure;
        }

        var targetDirectory = ResolveTargetDirectory(options);

        try
        {
            Directory.CreateDirectory(targetDirectory);
            var result = await backupService.CreateBackupAsync(new UpdateBackupRequest(targetDirectory, reason), cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogError("Sicherung vor dem Update fehlgeschlagen: {Message}", result.Message);
                return !options.CancelInstallationOnFailure;
            }

            _logger.LogInformation("Sicherung vor dem Update erstellt: {BackupFile}", result.BackupFilePath);
            ApplyRetention(targetDirectory, options.RetainedBackupCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sicherung vor dem Update konnte nicht erstellt werden.");
            return !options.CancelInstallationOnFailure;
        }
    }

    /// <summary>
    /// Resolves the configured backup directory to an absolute path.
    /// </summary>
    /// <param name="options">The backup configuration.</param>
    /// <returns>The absolute backup directory.</returns>
    private string ResolveTargetDirectory(UpdateBackupOptions options)
    {
        var configuredPath = string.IsNullOrWhiteSpace(options.Path) ? "Backups" : options.Path;
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath);
    }

    /// <summary>
    /// Deletes the oldest backup files so that at most <paramref name="retainedBackupCount"/> files remain.
    /// </summary>
    /// <param name="directory">The backup directory.</param>
    /// <param name="retainedBackupCount">The number of backups to keep; zero or less keeps all backups.</param>
    private void ApplyRetention(string directory, int retainedBackupCount)
    {
        if (retainedBackupCount <= 0)
        {
            return;
        }

        var obsoleteBackups = new DirectoryInfo(directory)
            .GetFiles("*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Skip(retainedBackupCount);

        foreach (var backup in obsoleteBackups)
        {
            try
            {
                backup.Delete();
                _logger.LogInformation("Alte Sicherung gelöscht: {BackupFile}", backup.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alte Sicherung konnte nicht gelöscht werden: {BackupFile}", backup.FullName);
            }
        }
    }
}
