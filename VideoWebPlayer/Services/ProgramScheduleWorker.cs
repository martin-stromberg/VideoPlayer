using Microsoft.Extensions.Hosting;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services;

/// <summary>
/// Background worker that keeps the automated TV channel's program schedule covered: after a
/// start-up delay it repeatedly calls <see cref="IProgramScheduleService.EnsureCoverageAsync"/>
/// and then sleeps for the configured generation interval (re-read each iteration, so changes
/// apply without a restart). Each run happens inside a fresh scope behind the
/// <see cref="IBackgroundProcessingGate"/> so a backup restore is never run over.
/// </summary>
public sealed class ProgramScheduleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProgramScheduleWorker> _logger;
    private readonly IProgramScheduleSignal? _signal;
    private readonly TimeSpan _initialDelay;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgramScheduleWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve the scoped schedule service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="signal">Optional wake-up line fired by rule CRUD and media changes; when
    /// <see langword="null"/> the worker only runs on the interval.</param>
    /// <param name="initialDelay">Optional delay before the first run (default 15 seconds).</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public ProgramScheduleWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProgramScheduleWorker> logger,
        IProgramScheduleSignal? signal = null,
        TimeSpan? initialDelay = null,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _signal = signal;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(15);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ProgramScheduleWorker] Started");

        try
        {
            if (_initialDelay > TimeSpan.Zero)
                await Task.Delay(_initialDelay, _timeProvider, stoppingToken);

            Task? signalTask = null;
            while (!stoppingToken.IsCancellationRequested)
            {
                var interval = TimeSpan.FromMinutes(15);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
                    interval = await settings.GetProgramScheduleIntervalAsync(stoppingToken);

                    if (await settings.GetTvSenderEnabledAsync(stoppingToken))
                    {
                        var gate = scope.ServiceProvider.GetService<IBackgroundProcessingGate>();
                        await using var lease = gate is null
                            ? null
                            : await gate.EnterOperationAsync("Sendeplan", stoppingToken);

                        var service = scope.ServiceProvider.GetRequiredService<IProgramScheduleService>();
                        var pass = await service.ExecutePassAsync(_timeProvider.GetUtcNow(), stoppingToken);
                        // Materialized changes wake the live stream immediately (dedicated
                        // channel — the shared one is consumed by this worker itself, Issue
                        // #315): a slot displaced mid-air must reach the player at once, not
                        // up to 30 s later.
                        if (_signal is not null
                            && (pass.Reconcile.SlotsDisplaced > 0 || pass.Reconcile.SlotsCreated > 0 || pass.Coverage.SlotsCreated > 0))
                            _signal.NotifyLiveScheduleChanged();
                    }
                    // Feature-Flag aus: Sendeplan pausiert (kein Reconcile, kein Ausbau) —
                    // der Intervall-Sleep unten bleibt, das Aufwachen per Signal ebenfalls.
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ProgramScheduleWorker] Fehler bei der Sendeplan-Nachfuehrung.");
                }

                // Wake on the configured interval or when a schedule-relevant change was signaled —
                // whichever comes first. The signal task survives iterations: a fresh
                // ReadAsync per pass would leave a ghost reader in the channel queue that
                // consumes the next signal while nobody awaits it (Issue #315).
                var delayTask = Task.Delay(interval, _timeProvider, stoppingToken);
                if (_signal is null)
                {
                    await delayTask;
                }
                else
                {
                    signalTask ??= _signal.WaitAsync(stoppingToken).AsTask();
                    await Task.WhenAny(delayTask, signalTask);
                    if (signalTask.IsCompleted)
                    {
                        await signalTask;
                        signalTask = null;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation("[ProgramScheduleWorker] Stopped");
    }
}
