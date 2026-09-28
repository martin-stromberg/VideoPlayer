using msTools.Updater;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Facade for update administration UI state and manual update actions.
/// </summary>
public sealed class UpdateAdminService
{
    private static readonly AutoUpdateState[] BusyStates =
    [
        AutoUpdateState.Checking,
        AutoUpdateState.Downloading,
        AutoUpdateState.Installing
    ];

    private readonly IAutoUpdateOrchestrator _orchestrator;
    private readonly IAutoUpdateCommandHandler _commands;
    private readonly IUpdateSettingsService _settingsService;
    private readonly ILogger<UpdateAdminService> _logger;

    /// <summary>
    /// Creates a new admin facade.
    /// </summary>
    /// <param name="orchestrator">The auto-update orchestrator providing the current status.</param>
    /// <param name="commands">The command handler executing checks, downloads and installations.</param>
    /// <param name="settingsService">The service persisting the update settings.</param>
    /// <param name="logger">The logger.</param>
    public UpdateAdminService(
        IAutoUpdateOrchestrator orchestrator,
        IAutoUpdateCommandHandler commands,
        IUpdateSettingsService settingsService,
        ILogger<UpdateAdminService> logger)
    {
        _orchestrator = orchestrator;
        _commands = commands;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets settings and current updater status for the admin UI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The settings, the updater status and the default settings.</returns>
    public async Task<UpdateAdminSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetOrCreateAsync(cancellationToken);
        var status = await _orchestrator.GetStatusAsync(cancellationToken);
        var defaultSettings = _settingsService.GetDefaultSettings();
        return new UpdateAdminSnapshot(settings, status, defaultSettings);
    }

    /// <summary>
    /// Updates settings.
    /// </summary>
    /// <param name="update">The new settings values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted settings.</returns>
    public Task<UpdateSettings> UpdateSettingsAsync(UpdateSettingsUpdate update, CancellationToken cancellationToken = default)
        => _settingsService.UpdateAsync(update, cancellationToken);

    /// <summary>
    /// Triggers an immediate update check.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the check, or a blocked result while another update action is running.</returns>
    public async Task<UpdateAdminActionResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        await _settingsService.ApplyToRuntimeOptionsAsync(cancellationToken);
        var status = await _orchestrator.GetStatusAsync(cancellationToken);
        if (IsBusy(status))
            return UpdateAdminActionResult.Blocked("Es läuft bereits eine Update-Aktion.");

        return ToActionResult(await _commands.CheckAsync(cancellationToken));
    }

    /// <summary>
    /// Downloads and installs the known update package.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the installation, or a blocked/failed result.</returns>
    public async Task<UpdateAdminActionResult> InstallAsync(CancellationToken cancellationToken = default)
    {
        await _settingsService.ApplyToRuntimeOptionsAsync(cancellationToken);
        var status = await _orchestrator.GetStatusAsync(cancellationToken);
        if (IsBusy(status))
            return UpdateAdminActionResult.Blocked("Es läuft bereits eine Update-Aktion.");

        if (!IsInstallable(status))
            return UpdateAdminActionResult.Blocked("Es ist keine installierbare Version bekannt.");

        try
        {
            if (status.State == AutoUpdateState.UpdateAvailable)
            {
                var download = await _commands.DownloadAsync(cancellationToken);
                if (download.Outcome != AutoUpdateOutcome.Success)
                    return ToActionResult(download);
            }

            return ToActionResult(await _commands.InstallAsync(true, false, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual update installation failed.");
            return UpdateAdminActionResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Returns whether a status blocks additional manual actions.
    /// </summary>
    /// <param name="status">The updater status snapshot.</param>
    /// <returns><see langword="true"/> while a check, download or installation is running or the updater is locked.</returns>
    public static bool IsBusy(AutoUpdateStatusSnapshot status)
        => status.IsLocked || BusyStates.Contains(status.State);

    /// <summary>
    /// Returns whether the current status can be installed manually.
    /// </summary>
    /// <param name="status">The updater status snapshot.</param>
    /// <returns><see langword="true"/> if a known version can be installed.</returns>
    public static bool IsInstallable(AutoUpdateStatusSnapshot status)
        => status.State is AutoUpdateState.UpdateAvailable or AutoUpdateState.ReadyToInstall
           && !string.IsNullOrWhiteSpace(status.AvailableVersion ?? status.LastCheckResult?.AvailableVersion);

    private static UpdateAdminActionResult ToActionResult(AutoUpdateResult result)
    {
        if (result.Outcome == AutoUpdateOutcome.Failed)
            return UpdateAdminActionResult.Failed(result.Error?.Message ?? result.Message ?? "Update-Aktion fehlgeschlagen.");

        if (result.Outcome is AutoUpdateOutcome.Skipped or AutoUpdateOutcome.Canceled)
            return UpdateAdminActionResult.Blocked(result.Message ?? "Update-Aktion wurde übersprungen.");

        return UpdateAdminActionResult.Success(result.Message ?? "Update-Aktion abgeschlossen.");
    }
}

/// <summary>
/// Combines persisted settings and updater status.
/// </summary>
/// <param name="Settings">The persisted settings.</param>
/// <param name="Status">The current updater status.</param>
/// <param name="DefaultSettings">The default settings.</param>
/// <returns>The snapshot value.</returns>
public sealed record UpdateAdminSnapshot(UpdateSettings Settings, AutoUpdateStatusSnapshot Status, UpdateSettings DefaultSettings);

/// <summary>
/// Describes the result of a manual update action.
/// </summary>
/// <param name="Succeeded">Whether the action succeeded.</param>
/// <param name="IsBlocked">Whether the action was blocked (for example by a running action).</param>
/// <param name="Message">The message shown to the administrator.</param>
/// <returns>The result value.</returns>
public sealed record UpdateAdminActionResult(bool Succeeded, bool IsBlocked, string Message)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="message">The message shown to the administrator.</param>
    /// <returns>The result.</returns>
    public static UpdateAdminActionResult Success(string message)
        => new(true, false, message);

    /// <summary>
    /// Creates a blocked result.
    /// </summary>
    /// <param name="message">The message shown to the administrator.</param>
    /// <returns>The result.</returns>
    public static UpdateAdminActionResult Blocked(string message)
        => new(false, true, message);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="message">The message shown to the administrator.</param>
    /// <returns>The result.</returns>
    public static UpdateAdminActionResult Failed(string message)
        => new(false, false, message);
}
