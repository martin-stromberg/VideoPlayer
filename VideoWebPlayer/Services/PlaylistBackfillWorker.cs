using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services;

/// <summary>
/// Background worker driving the automatic playlist backfill mechanism (Entwicklungsschritt 8): periodically
/// calls <see cref="PlaylistBackfillService.RunBatchAsync"/> so that playlists containing a complete TV show,
/// TV show season or movie collection automatically pick up newly added children (a new season, episode or
/// movie) without the user having to re-add the collection. Modeled after <c>MediaSourceScanService</c>
/// (configurable interval, checked once per short internal loop tick so configuration changes are picked up
/// reasonably quickly) rather than <c>ActorBackfillWorker</c> (single run on startup): this mechanism must
/// keep running for the lifetime of the application, not just once after an upgrade.
/// </summary>
public sealed class PlaylistBackfillWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlaylistBackfillWorker> _logger;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _loopDelay;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistBackfillWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve scoped services (database context, settings) per iteration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="initialDelay">Optional delay before the first loop iteration, primarily for testing.</param>
    /// <param name="loopDelay">Optional delay between internal loop ticks, primarily for testing.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public PlaylistBackfillWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PlaylistBackfillWorker> logger,
        TimeSpan? initialDelay = null,
        TimeSpan? loopDelay = null,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(30);
        _loopDelay = loopDelay ?? TimeSpan.FromMinutes(1);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PlaylistBackfillWorker] Started");

        if (_initialDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(_initialDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        var lastRun = DateTimeOffset.MinValue;

        // Resumed round-robin cursor into the "playlists with a collection entry" sweep (see
        // PlaylistBackfillService.RunBatchAsync); intentionally in-memory only (reset to 0 - "start from the
        // beginning" - on every application restart) rather than persisted, since a restart is infrequent
        // and losing the exact cursor position merely means the next run or two re-examines some playlists
        // that were already caught up, which is harmless and bounded by BackfillBatchSize either way.
        var lastProcessedPlaylistId = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = _timeProvider.GetUtcNow();
                using var scope = _scopeFactory.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<IOptions<PlaylistSettings>>().Value;
                var interval = TimeSpan.FromMinutes(Math.Max(1, settings.BackfillIntervalMinutes));

                if (now - lastRun >= interval)
                {
                    lastRun = now;

                    var gate = scope.ServiceProvider.GetService<IBackgroundProcessingGate>();
                    await using var processingLease = gate is null ? null : await gate.EnterOperationAsync("PlaylistBackfill", stoppingToken);

                    var backfillService = scope.ServiceProvider.GetRequiredService<PlaylistBackfillService>();
                    var batchSize = Math.Max(1, settings.BackfillBatchSize);

                    var result = await backfillService.RunBatchAsync(lastProcessedPlaylistId, batchSize, stoppingToken);
                    lastProcessedPlaylistId = result.PlaylistsExamined > 0 ? result.LastProcessedPlaylistId : 0;

                    if (result.EntriesAdded > 0)
                    {
                        _logger.LogInformation(
                            "[PlaylistBackfillWorker] {Added} Titel in {Playlists} geprueften Playlists automatisch nachgeliefert.",
                            result.EntriesAdded, result.PlaylistsExamined);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "[PlaylistBackfillWorker] Durchlauf abgeschlossen ({Playlists} Playlists geprueft, nichts nachzuliefern).",
                            result.PlaylistsExamined);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlaylistBackfillWorker] Fehler beim automatischen Nachliefern von Playlist-Titeln.");
            }

            if (_loopDelay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(_loopDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("[PlaylistBackfillWorker] Stopped");
    }
}
