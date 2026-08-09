using msTools.Updater;

namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Connects the updater's pre-install event to <see cref="UpdateBackupCoordinator"/> so that a full data export
/// is created before a new program version is installed. The installation is canceled when the backup fails and
/// <see cref="UpdateBackupOptions.CancelInstallationOnFailure"/> is set.
/// </summary>
public sealed class UpdateBackupEventBinder : IHostedService
{
    private readonly IAutoUpdateEventAggregator _events;
    private readonly UpdateBackupCoordinator _coordinator;
    private readonly ILogger<UpdateBackupEventBinder> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="events">The updater's event aggregator.</param>
    /// <param name="coordinator">Creates the backup and applies the configured retention.</param>
    /// <param name="logger">Logger instance.</param>
    public UpdateBackupEventBinder(
        IAutoUpdateEventAggregator events,
        UpdateBackupCoordinator coordinator,
        ILogger<UpdateBackupEventBinder> logger)
    {
        _events = events;
        _coordinator = coordinator;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _events.BeforeInstall += OnBeforeInstall;
        _events.ErrorOccurred += OnErrorOccurred;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _events.BeforeInstall -= OnBeforeInstall;
        _events.ErrorOccurred -= OnErrorOccurred;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates the backup before the update is installed. The event is synchronous, so the asynchronous backup is
    /// awaited blocking; the updater raises the event from a background workflow.
    /// </summary>
    /// <param name="sender">The updater component raising the event.</param>
    /// <param name="args">The pending installation, which can be canceled.</param>
    private void OnBeforeInstall(object? sender, BeforeInstallEventArgs args)
    {
        var reason = $"Programmupdate: {args.PackageFile.Name}";
        var mayProceed = _coordinator.CreateBackupAsync(reason).GetAwaiter().GetResult();
        if (mayProceed)
        {
            return;
        }

        args.Cancel = true;
        _logger.LogError("Installation des Updates abgebrochen, da keine Sicherung erstellt werden konnte.");
    }

    /// <summary>
    /// Logs errors reported by the updater.
    /// </summary>
    /// <param name="sender">The updater component raising the event.</param>
    /// <param name="args">The reported error.</param>
    private void OnErrorOccurred(object? sender, AutoUpdateErrorEventArgs args)
        => _logger.LogError(args.Error, "Automatisches Update fehlgeschlagen (Phase {Phase}).", args.Phase);
}
