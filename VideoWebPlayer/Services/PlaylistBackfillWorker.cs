using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Background worker for the automatic playlist backfill (Entwicklungsschritt 8). It does NOT poll: it sleeps
/// and is woken by exactly three things, each of which hands the actual work to
/// <see cref="PlaylistBackfillCoordinator"/>:
/// <list type="number">
/// <item>the end of a scan, or media created outside a scan (<see cref="IPlaylistBackfillSignal"/>) - after a
/// short settle time it processes the markers (<see cref="PlaylistBackfillCoordinator.ProcessPendingAsync"/>);
/// with nothing marked that is a single cheap query;</item>
/// <item>the start of the application - once, after a start-up delay, it processes markers still open from
/// before the last shutdown and catches up an overdue safety sweep;</item>
/// <item>the daily safety sweep (<see cref="PlaylistSettings.BackfillSafetySweepIntervalHours"/>), scheduled with a
/// single timer for the persisted due time - not with a periodic tick.</item>
/// </list>
/// There is no periodic pass over all playlists any more.
/// </summary>
public sealed class PlaylistBackfillWorker : BackgroundService
{
    // Task.Delay cannot wait longer than about 49 days; a longer wait simply re-evaluates the due time afterwards.
    private static readonly TimeSpan MaxTimerWait = TimeSpan.FromDays(24);

    // After a failed safety sweep (infrastructure error, not a single playlist) wait this long before trying again.
    private static readonly TimeSpan SweepRetryDelay = TimeSpan.FromHours(1);

    private readonly PlaylistBackfillCoordinator _coordinator;
    private readonly IPlaylistBackfillSignal _signal;
    private readonly IOptions<PlaylistSettings> _settings;
    private readonly ILogger<PlaylistBackfillWorker> _logger;
    private readonly TimeSpan _initialDelay;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistBackfillWorker"/> class.
    /// </summary>
    /// <param name="coordinator">Runs the actual backfill work units.</param>
    /// <param name="signal">The wake-up line raised by scans and by media creation.</param>
    /// <param name="settings">Playlist configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="initialDelay">Optional delay before the start-up catch-up, primarily for testing (default 30 seconds).</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public PlaylistBackfillWorker(
        PlaylistBackfillCoordinator coordinator,
        IPlaylistBackfillSignal signal,
        IOptions<PlaylistSettings> settings,
        ILogger<PlaylistBackfillWorker> logger,
        TimeSpan? initialDelay = null,
        TimeProvider? timeProvider = null)
    {
        _coordinator = coordinator;
        _signal = signal;
        _settings = settings;
        _logger = logger;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(30);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PlaylistBackfillWorker] Started");

        try
        {
            if (_initialDelay > TimeSpan.Zero)
                await Task.Delay(_initialDelay, stoppingToken);

            // Start-up catch-up: markers left over from before the last shutdown, and an overdue safety sweep.
            await GuardedAsync(() => _coordinator.ProcessPendingAsync(stoppingToken), "Verarbeitung offener Markierungen");
            var sweepRetryAt = await GuardedSweepAsync(stoppingToken);

            Task? signalTask = null;
            while (!stoppingToken.IsCancellationRequested)
            {
                signalTask ??= _signal.WaitAsync(stoppingToken).AsTask();

                using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var timerTask = Task.Delay(await NextSweepWaitAsync(sweepRetryAt, stoppingToken), _timeProvider, timerCts.Token);

                var completed = await Task.WhenAny(signalTask, timerTask);
                timerCts.Cancel();
                stoppingToken.ThrowIfCancellationRequested();

                if (completed == signalTask)
                {
                    await signalTask;
                    signalTask = null;

                    var settle = TimeSpan.FromSeconds(Math.Max(0, _settings.Value.BackfillSettleSeconds));
                    if (settle > TimeSpan.Zero)
                        await Task.Delay(settle, _timeProvider, stoppingToken);

                    await GuardedAsync(() => _coordinator.ProcessPendingAsync(stoppingToken), "Verarbeitung markierter Sammel-Medien");
                }
                else
                {
                    sweepRetryAt = await GuardedSweepAsync(stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation("[PlaylistBackfillWorker] Stopped");
    }

    // How long to sleep until the safety sweep is due (never shorter than a pending retry back-off);
    // forever when the sweep is switched off.
    private async Task<TimeSpan> NextSweepWaitAsync(DateTimeOffset? sweepRetryAt, CancellationToken cancellationToken)
    {
        DateTimeOffset? dueAt;
        try
        {
            dueAt = await _coordinator.GetSafetySweepDueAtAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[PlaylistBackfillWorker] Faelligkeit des Sicherheitslaufs konnte nicht ermittelt werden.");
            return SweepRetryDelay;
        }

        if (dueAt is null)
            return Timeout.InfiniteTimeSpan;

        var due = sweepRetryAt is { } retryAt && retryAt > dueAt ? retryAt : dueAt.Value;
        var wait = due - _timeProvider.GetUtcNow();
        if (wait < TimeSpan.Zero)
            wait = TimeSpan.Zero;
        return wait > MaxTimerWait ? MaxTimerWait : wait;
    }

    // Runs the safety sweep if due, containing errors; returns the earliest time to try again after a failure.
    private async Task<DateTimeOffset?> GuardedSweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _coordinator.RunSafetySweepIfDueAsync(cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[PlaylistBackfillWorker] Fehler beim taeglichen Sicherheitslauf.");
            return _timeProvider.GetUtcNow() + SweepRetryDelay;
        }
    }

    private async Task GuardedAsync(Func<Task> work, string what)
    {
        try
        {
            await work();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[PlaylistBackfillWorker] Fehler bei: {What}.", what);
        }
    }
}
