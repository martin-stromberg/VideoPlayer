using Microsoft.Extensions.Hosting;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services;

/// <summary>
/// Background worker that keeps the automated TV channel's live stream running: after a start-up
/// delay and a one-time cleanup of the segment directory it repeatedly lets
/// <see cref="LiveStreamCoordinator"/> resynchronize the ffmpeg segmentation with the schedule.
/// Each tick runs in a fresh scope behind the <see cref="IBackgroundProcessingGate"/>, wakes on
/// schedule changes through <see cref="IProgramScheduleSignal"/> and otherwise sleeps until the
/// next decision point (slot end/start) or the poll cap.
/// </summary>
public sealed class LiveStreamWorker : BackgroundService
{
    /// <summary>The maximum time between two synchronization ticks.</summary>
    internal static readonly TimeSpan PollCap = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LiveStreamWorker> _logger;
    private readonly LiveStreamStandbyProvider _standbyProvider;
    private readonly IProgramScheduleSignal? _signal;
    private readonly TimeSpan _initialDelay;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastFfmpegWarning = DateTimeOffset.MinValue;
    private (LiveStreamSyncAction Action, string? Detail) _lastPersistentResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveStreamWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve the scoped coordinator.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="standbyProvider">The in-memory standby stream provider (singleton).</param>
    /// <param name="signal">Optional wake-up line fired by schedule-relevant changes.</param>
    /// <param name="initialDelay">Optional delay before the first run (default 10 seconds).</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public LiveStreamWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<LiveStreamWorker> logger,
        LiveStreamStandbyProvider standbyProvider,
        IProgramScheduleSignal? signal = null,
        TimeSpan? initialDelay = null,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _standbyProvider = standbyProvider;
        _signal = signal;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(10);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[LiveStreamWorker] Started");

        try
        {
            // Leftovers of a previous run carry no usable program point — clear the segment
            // directory BEFORE the start-up delay so stale playlists/segments can no longer
            // be delivered during warm-up (the standby stream answers in the meantime).
            using (var scope = _scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<LiveStreamSegmentStore>();
                await store.ClearAsync(stoppingToken);
            }

            // Kick off the standby preparation: a configured clip is transcoded once into the
            // disk cache; a failure is tolerated — the embedded default covers the warm-up.
            try
            {
                await _standbyProvider.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LiveStreamWorker] Standby-Vorbereitung fehlgeschlagen — der Standard-Clip greift weiter.");
            }

            if (_initialDelay > TimeSpan.Zero)
                await Task.Delay(_initialDelay, _timeProvider, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = PollCap;
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();

                    if (!await settings.GetTvSenderEnabledAsync(stoppingToken))
                    {
                        // Feature-Flag aus: keine Segmentierung — einen evtl. noch laufenden
                        // ffmpeg-Lauf stoppen (idempotent), dann den Poll-Takt beibehalten.
                        var segmenter = scope.ServiceProvider.GetRequiredService<ILiveStreamSegmenter>();
                        if (segmenter.IsRunning)
                            await segmenter.StopAsync(stoppingToken);
                    }
                    else
                    {
                        var gate = scope.ServiceProvider.GetService<IBackgroundProcessingGate>();
                        await using var lease = gate is null
                            ? null
                            : await gate.EnterOperationAsync("Live-Stream", stoppingToken);

                        var coordinator = scope.ServiceProvider.GetRequiredService<LiveStreamCoordinator>();
                        var result = await coordinator.SynchronizeAsync(_timeProvider.GetUtcNow(), stoppingToken);

                        // Nur unmittelbar aufeinanderfolgende Gleichergebnisse duerfen
                        // dedupliziert werden — ein Zwischenzustand (z. B. Started oder
                        // eine Sendelücke) macht das naechste MediaUnavailable wieder zu
                        // einem neuen Ereignis, nicht zu einer Wiederholung.
                        if (result.Action is not (LiveStreamSyncAction.MediaUnavailable or LiveStreamSyncAction.InputUnplayable))
                            _lastPersistentResult = default;

                        if (result.Action == LiveStreamSyncAction.FfmpegUnavailable)
                            LogFfmpegUnavailableThrottled(result.Detail);
                        else if (result.Action == LiveStreamSyncAction.Started)
                            _logger.LogInformation("[LiveStreamWorker] {Detail}", result.Detail);
                        else if (result.Action is LiveStreamSyncAction.MediaUnavailable or LiveStreamSyncAction.InputUnplayable)
                        {
                            // Dauerzustände (z. B. ein übersprungener Sendeplatz) wiederholen
                            // sich im Poll-Takt — nur das erste Auftreten ist Information,
                            // unveränderte Folgeergebnisse gehen auf Debug.
                            var key = (result.Action, result.Detail);
                            if (key.Equals(_lastPersistentResult))
                                _logger.LogDebug("[LiveStreamWorker] {Detail}", result.Detail);
                            else
                                _logger.LogInformation("[LiveStreamWorker] {Detail}", result.Detail);
                            _lastPersistentResult = key;
                        }

                        if (result.WakeAtUtc is { } wakeAt)
                        {
                            var until = wakeAt - _timeProvider.GetUtcNow();
                            if (until < delay)
                                delay = until > TimeSpan.Zero ? until : TimeSpan.FromSeconds(1);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[LiveStreamWorker] Fehler bei der Live-Stream-Synchronisation.");
                }

                // Wake on the computed delay or when a schedule-relevant change was signaled —
                // whichever comes first.
                var delayTask = Task.Delay(delay, _timeProvider, stoppingToken);
                if (_signal is null)
                {
                    await delayTask;
                }
                else
                {
                    var signalTask = _signal.WaitAsync(stoppingToken).AsTask();
                    await Task.WhenAny(delayTask, signalTask);
                    if (signalTask.IsCompleted)
                        await signalTask;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation("[LiveStreamWorker] Stopped");
    }

    private void LogFfmpegUnavailableThrottled(string? detail)
    {
        var now = _timeProvider.GetUtcNow();
        if (now - _lastFfmpegWarning < TimeSpan.FromMinutes(5))
            return;
        _lastFfmpegWarning = now;
        _logger.LogWarning("[LiveStreamWorker] {Detail} Der Live-Stream pausiert, bis ffmpeg verfuegbar ist.", detail);
    }
}
