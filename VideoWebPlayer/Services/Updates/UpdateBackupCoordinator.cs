namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Coordinates the data backup that is created before a program update is installed: resolves the optional
/// <see cref="IUpdateBackupService"/> and provides the configured target directory for backup providers that
/// write update backups themselves.
/// </summary>
public sealed class UpdateBackupCoordinator
{
    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<UpdateBackupCoordinator> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="services">Used to resolve the optional <see cref="IUpdateBackupService"/> in its own scope.</param>
    /// <param name="environment">Used to resolve a relative backup path against the content root.</param>
    /// <param name="logger">Logger instance.</param>
    public UpdateBackupCoordinator(
        IServiceProvider services,
        IHostEnvironment environment,
        ILogger<UpdateBackupCoordinator> logger)
    {
        _services = services;
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
        using var scope = _services.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<IUpdateSettingsService>();
        var options = await settingsService.GetBackupOptionsAsync(cancellationToken);
        if (!options.Enabled)
        {
            _logger.LogInformation("Update-Backup ist deaktiviert; es wird keine Sicherung erstellt.");
            return true;
        }

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
            var result = await backupService.CreateBackupAsync(new UpdateBackupRequest(targetDirectory, reason), cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogError("Sicherung vor dem Update fehlgeschlagen: {Message}", result.Message);
                return !options.CancelInstallationOnFailure;
            }

            _logger.LogInformation("Sicherung vor dem Update erstellt: {BackupFile}", result.BackupFilePath);
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
}
