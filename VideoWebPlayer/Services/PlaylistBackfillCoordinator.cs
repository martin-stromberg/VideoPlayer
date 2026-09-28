using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services;

/// <summary>
/// The outcome of one <see cref="PlaylistBackfillCoordinator"/> run.
/// </summary>
/// <param name="MarkersSeen">How many markers were pending when the run started (always 0 for a safety sweep, which ignores markers).</param>
/// <param name="PlaylistsExamined">How many playlists were examined.</param>
/// <param name="EntriesAdded">How many playlist entries were added.</param>
/// <param name="PlaylistsFailed">How many playlists failed (their markers stay for the next attempt).</param>
/// <returns>A value describing how much work a run did.</returns>
public readonly record struct PlaylistBackfillRunSummary(int MarkersSeen, int PlaylistsExamined, int EntriesAdded, int PlaylistsFailed);

/// <summary>
/// Orchestrates the automatic playlist backfill in bounded work units: <see cref="ProcessPendingAsync"/> handles
/// the markers written by <see cref="ApplicationDbContext"/> (only the playlists that contain a marked
/// collection medium), <see cref="RunSafetySweepAsync"/> is the once-a-day full check of every playlist with a
/// collection entry. Both work through the playlists in blocks of <see cref="PlaylistSettings.BackfillBatchSize"/>
/// with a pause of <see cref="PlaylistSettings.BackfillBlockPauseSeconds"/> between blocks, each block in its own
/// scope (own database context, own <see cref="IBackgroundProcessingGate"/> lease so a backup or restore is never
/// run over), so even a large catch-up creates no load spike. The per-playlist logic itself is
/// <see cref="PlaylistBackfillService"/>. When to run is decided by <see cref="PlaylistBackfillWorker"/>.
/// </summary>
public sealed class PlaylistBackfillCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PlaylistSettings> _settings;
    private readonly ILogger<PlaylistBackfillCoordinator> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _pause;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistBackfillCoordinator"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve scoped services (database context, backfill service) per work unit.</param>
    /// <param name="settings">Playlist configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    /// <param name="pause">Optional replacement for the pause between blocks, primarily for testing.</param>
    public PlaylistBackfillCoordinator(
        IServiceScopeFactory scopeFactory,
        IOptions<PlaylistSettings> settings,
        ILogger<PlaylistBackfillCoordinator> logger,
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? pause = null)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _pause = pause ?? ((delay, token) => Task.Delay(delay, token));
    }

    private int BlockSize => Math.Max(1, _settings.Value.BackfillBatchSize);

    private TimeSpan BlockPause => TimeSpan.FromSeconds(Math.Max(0, _settings.Value.BackfillBlockPauseSeconds));

    /// <summary>
    /// Processes the pending markers: finds exactly the playlists that contain a marked collection medium,
    /// backfills them block by block, then removes the processed markers (only at the processed version, and
    /// not those of a failed playlist). With nothing marked this costs a single cheap query.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token. On cancellation the markers simply stay.</param>
    /// <returns>What the run did.</returns>
    public async Task<PlaylistBackfillRunSummary> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        PlaylistBackfillPlan plan;
        await using (var lease = await EnterGateAsync(cancellationToken))
        {
            using var scope = _scopeFactory.CreateScope();
            plan = await scope.ServiceProvider.GetRequiredService<PlaylistBackfillService>().PlanPendingAsync(cancellationToken);
        }

        if (plan.IsEmpty)
            return default;

        var examined = 0;
        var added = 0;
        var failed = new List<long>();

        var blockIndex = 0;
        foreach (var block in plan.PlaylistIds.Chunk(BlockSize))
        {
            if (blockIndex++ > 0)
                await PauseBetweenBlocksAsync(cancellationToken);

            var result = await RunBlockAsync(block, cancellationToken);
            examined += result.PlaylistsExamined;
            added += result.EntriesAdded;
            failed.AddRange(result.FailedPlaylistIds);
        }

        await using (var lease = await EnterGateAsync(cancellationToken))
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PlaylistBackfillService>()
                .ReleaseMarkersAsync(plan, failed, cancellationToken);
        }

        var summary = new PlaylistBackfillRunSummary(plan.Markers.Count, examined, added, failed.Count);
        LogSummary("markierte Sammel-Medien", summary);
        return summary;
    }

    /// <summary>
    /// Runs the safety sweep unconditionally: checks every playlist that contains a TV show, season or movie
    /// collection block by block, then stores the completion time (persistently) so the next sweep is due one
    /// interval later. Markers are neither read nor removed, so the sweep cannot lose any.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token. A cancelled sweep is not recorded as done and is caught up after the next start.</param>
    /// <returns>What the sweep did.</returns>
    public async Task<PlaylistBackfillRunSummary> RunSafetySweepAsync(CancellationToken cancellationToken)
    {
        var examined = 0;
        var added = 0;
        var failedCount = 0;
        var afterId = 0L;
        var blockIndex = 0;

        while (true)
        {
            List<long> block;
            await using (var lease = await EnterGateAsync(cancellationToken))
            {
                using var scope = _scopeFactory.CreateScope();
                block = await scope.ServiceProvider.GetRequiredService<PlaylistBackfillService>()
                    .LoadSweepBlockAsync(afterId, BlockSize, cancellationToken);
            }

            if (block.Count == 0)
                break;

            if (blockIndex++ > 0)
                await PauseBetweenBlocksAsync(cancellationToken);

            var result = await RunBlockAsync(block, cancellationToken);
            examined += result.PlaylistsExamined;
            added += result.EntriesAdded;
            failedCount += result.FailedPlaylistIds.Count;
            afterId = block[^1];
        }

        await SetLastSweepAsync(_timeProvider.GetUtcNow(), cancellationToken);

        var summary = new PlaylistBackfillRunSummary(0, examined, added, failedCount);
        LogSummary("Sicherheitslauf", summary);
        return summary;
    }

    /// <summary>
    /// Runs the safety sweep if it is enabled (<see cref="PlaylistSettings.BackfillSafetySweepIntervalHours"/> above 0) and due.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if a sweep ran.</returns>
    public async Task<bool> RunSafetySweepIfDueAsync(CancellationToken cancellationToken)
    {
        var dueAt = await GetSafetySweepDueAtAsync(cancellationToken);
        if (dueAt is null || dueAt > _timeProvider.GetUtcNow())
            return false;

        await RunSafetySweepAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Gets the time the next safety sweep is due: the persisted time of the last completed sweep plus the
    /// configured interval, or "now" if it never ran (or if the stored time lies in the future, i.e. after a
    /// system clock that was set ahead and back).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The due time, or <c>null</c> when the sweep is switched off.</returns>
    public async Task<DateTimeOffset?> GetSafetySweepDueAtAsync(CancellationToken cancellationToken)
    {
        var hours = _settings.Value.BackfillSafetySweepIntervalHours;
        if (hours <= 0)
            return null;

        using var scope = _scopeFactory.CreateScope();
        var setup = await scope.ServiceProvider.GetRequiredService<ProgramSettingsService>().GetOrCreateSetupAsync(cancellationToken);
        if (setup.PlaylistBackfillLastSweepAt is not { } last)
            return DateTimeOffset.MinValue;

        var lastSweep = new DateTimeOffset(DateTime.SpecifyKind(last, DateTimeKind.Utc));

        // A stored time that lies in the future can only come from a system clock that was set ahead and back
        // again. Trusting it would suspend the safety net for as long as the clock had been off, so it counts
        // as "never ran" and the sweep is due at once (it then stores the correct time).
        if (lastSweep > _timeProvider.GetUtcNow())
            return DateTimeOffset.MinValue;

        return lastSweep.AddHours(hours);
    }

    private async Task SetLastSweepAsync(DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProgramSettingsService>().GetOrCreateSetupAsync(cancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var value = completedAt.UtcDateTime;
        await db.Setups.ExecuteUpdateAsync(s => s.SetProperty(x => x.PlaylistBackfillLastSweepAt, value), cancellationToken);
    }

    private async Task<PlaylistBackfillBlockResult> RunBlockAsync(IReadOnlyList<long> block, CancellationToken cancellationToken)
    {
        await using var lease = await EnterGateAsync(cancellationToken);
        using var scope = _scopeFactory.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<PlaylistBackfillService>()
            .BackfillPlaylistsAsync(block, cancellationToken);

        foreach (var playlistId in result.FailedPlaylistIds)
        {
            _logger.LogWarning(
                "[PlaylistBackfill] Playlist {PlaylistId} konnte nicht nachgeliefert werden; ihre Markierungen bleiben fuer den naechsten Versuch bestehen.",
                playlistId);
        }

        return result;
    }

    private async Task<IAsyncDisposable?> EnterGateAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var gate = scope.ServiceProvider.GetService<IBackgroundProcessingGate>();
        return gate is null ? null : await gate.EnterOperationAsync("PlaylistBackfill", cancellationToken);
    }

    private Task PauseBetweenBlocksAsync(CancellationToken cancellationToken)
        => BlockPause > TimeSpan.Zero ? _pause(BlockPause, cancellationToken) : Task.CompletedTask;

    private void LogSummary(string what, PlaylistBackfillRunSummary summary)
    {
        if (summary.EntriesAdded > 0)
        {
            _logger.LogInformation(
                "[PlaylistBackfill] {What}: {Added} Titel in {Playlists} Playlists nachgeliefert ({Failed} Playlists mit Fehler).",
                what, summary.EntriesAdded, summary.PlaylistsExamined, summary.PlaylistsFailed);
        }
        else
        {
            _logger.LogDebug(
                "[PlaylistBackfill] {What}: {Playlists} Playlists geprueft, nichts nachzuliefern ({Failed} Playlists mit Fehler).",
                what, summary.PlaylistsExamined, summary.PlaylistsFailed);
        }
    }
}
