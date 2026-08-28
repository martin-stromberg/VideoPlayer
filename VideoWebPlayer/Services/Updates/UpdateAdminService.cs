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
    public async Task<UpdateAdminSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetOrCreateAsync(cancellationToken);
        var status = await _orchestrator.GetStatusAsync(cancellationToken);
        return new UpdateAdminSnapshot(settings, status);
    }

    /// <summary>
    /// Updates settings.
    /// </summary>
    public Task<UpdateSettings> UpdateSettingsAsync(UpdateSettingsUpdate update, CancellationToken cancellationToken = default)
        => _settingsService.UpdateAsync(update, cancellationToken);

    /// <summary>
    /// Triggers an immediate update check.
    /// </summary>
    public async Task<UpdateAdminActionResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        await _settingsService.ApplyToRuntimeOptionsAsync(cancellationToken);
        var status = await _orchestrator.GetStatusAsync(cancellationToken);
        if (IsBusy(status))
            return UpdateAdminActionResult.Blocked("Es laeuft bereits eine Update-Aktion.");

        return ToActionResult(await _commands.CheckAsync(cancellationToken));
    }

    /// <summary>
    /// Downloads and installs the known update package.
    /// </summary>
    public async Task<UpdateAdminActionResult> InstallAsync(CancellationToken cancellationToken = default)
    {
        await _settingsService.ApplyToRuntimeOptionsAsync(cancellationToken);
        var status = await _orchestrator.GetStatusAsync(cancellationToken);
        if (IsBusy(status))
            return UpdateAdminActionResult.Blocked("Es laeuft bereits eine Update-Aktion.");

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
    public static bool IsBusy(AutoUpdateStatusSnapshot status)
        => status.IsLocked || BusyStates.Contains(status.State);

    /// <summary>
    /// Returns whether the current status can be installed manually.
    /// </summary>
    public static bool IsInstallable(AutoUpdateStatusSnapshot status)
        => status.State is AutoUpdateState.UpdateAvailable or AutoUpdateState.ReadyToInstall
           && !string.IsNullOrWhiteSpace(status.AvailableVersion ?? status.LastCheckResult?.AvailableVersion);

    private static UpdateAdminActionResult ToActionResult(AutoUpdateResult result)
    {
        if (result.Outcome == AutoUpdateOutcome.Failed)
            return UpdateAdminActionResult.Failed(result.Error?.Message ?? result.Message ?? "Update-Aktion fehlgeschlagen.");

        if (result.Outcome is AutoUpdateOutcome.Skipped or AutoUpdateOutcome.Canceled)
            return UpdateAdminActionResult.Blocked(result.Message ?? "Update-Aktion wurde uebersprungen.");

        return UpdateAdminActionResult.Success(result.Message ?? "Update-Aktion abgeschlossen.");
    }
}

/// <summary>
/// Combines persisted settings and updater status.
/// </summary>
public sealed record UpdateAdminSnapshot(UpdateSettings Settings, AutoUpdateStatusSnapshot Status);

/// <summary>
/// Describes the result of a manual update action.
/// </summary>
public sealed record UpdateAdminActionResult(bool Succeeded, bool IsBlocked, string Message)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static UpdateAdminActionResult Success(string message) => new(true, false, message);

    /// <summary>
    /// Creates a blocked result.
    /// </summary>
    public static UpdateAdminActionResult Blocked(string message) => new(false, true, message);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static UpdateAdminActionResult Failed(string message) => new(false, false, message);
}
