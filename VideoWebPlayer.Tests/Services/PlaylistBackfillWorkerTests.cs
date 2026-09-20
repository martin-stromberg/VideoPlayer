using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="PlaylistBackfillWorker"/>: it is driven by signals, by the start of the application
/// and by the daily safety sweep - and does nothing at all in between (no polling).
/// </summary>
public class PlaylistBackfillWorkerTests : PlaylistServiceTestBase, IDisposable
{
    private readonly PlaylistBackfillSignal _signal = new();
    private readonly SqlCommandRecorder _recorder = new();
    private readonly List<IDisposable> _disposables = new();

    public new void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
        base.Dispose();
    }

    private static PlaylistSettings Settings(int sweepHours) => new()
    {
        BackfillBatchSize = 5,
        BackfillBlockPauseSeconds = 0,
        BackfillSettleSeconds = 0,
        BackfillSafetySweepIntervalHours = sweepHours
    };

    private PlaylistBackfillWorker CreateWorker(PlaylistSettings settings)
    {
        var provider = PlaylistBackfillTestServices.Build(_connectionString, settings, _signal, null, _recorder);
        _disposables.Add(provider);
        var options = Microsoft.Extensions.Options.Options.Create(settings);
        var coordinator = new PlaylistBackfillCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<PlaylistBackfillCoordinator>.Instance);
        return new PlaylistBackfillWorker(coordinator, _signal, options, NullLogger<PlaylistBackfillWorker>.Instance, initialDelay: TimeSpan.Zero);
    }

    private ApplicationDbContext NewSignalledContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connectionString).Options;
        return new ApplicationDbContext(options, new EventManager(), _signal);
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, int timeoutMilliseconds = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return true;
            await Task.Delay(25);
        }

        return await condition();
    }

    // Waits until the worker's start-up catch-up is over and it is parked, i.e. the recorded SQL traffic stays quiet.
    private async Task WaitUntilIdleAsync()
    {
        Assert.True(await WaitUntilAsync(() => Task.FromResult(_recorder.Commands.Count > 0)), "worker never ran its start-up catch-up");
        var last = -1;
        while (last != _recorder.Commands.Count)
        {
            last = _recorder.Commands.Count;
            await Task.Delay(300);
        }
    }

    private async Task<(long ShowId, long PlaylistId)> CreateShowPlaylistAsync()
    {
        var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowSeason, seasonId),
            (MediaTypeValues.TVShowEpisode, episodeIds[0]));
        await ClearMarkersAsync();
        return (showId, playlistId);
    }

    private Task<int> EpisodeEntryCountAsync(long playlistId)
        => _db.PlaylistEntries.AsNoTracking().CountAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.TVShowEpisode);

    [Fact]
    public async Task Signal_TriggersTheProcessingOfMarkedCollections()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, playlistId) = await CreateShowPlaylistAsync();
        using var worker = CreateWorker(Settings(sweepHours: 0));
        await worker.StartAsync(ct);
        await WaitUntilIdleAsync();

        // Media appears outside a scan: the hook marks and signals, the worker delivers.
        await using (var db = NewSignalledContext())
        {
            var season = new TVShowSeason { Name = "Staffel 2", TVShowId = showId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
            db.TVShowSeasons.Add(season);
            await db.SaveChangesAsync(ct);
            db.TVShowEpisodes.Add(new TVShowEpisode { Name = "Folge", TVShowSeasonId = season.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = 1 });
            await db.SaveChangesAsync(ct);
        }

        Assert.True(await WaitUntilAsync(async () => await EpisodeEntryCountAsync(playlistId) == 2));
        Assert.True(await WaitUntilAsync(async () => !(await GetMarkersAsync()).Any()));
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DuringAScan_NothingHappensUntilTheScanEnds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, playlistId) = await CreateShowPlaylistAsync();
        using var worker = CreateWorker(Settings(sweepHours: 0));
        await worker.StartAsync(ct);
        await WaitUntilIdleAsync();

        var scan = _signal.BeginScan();
        await using (var db = NewSignalledContext())
        {
            var season = new TVShowSeason { Name = "Staffel 2", TVShowId = showId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
            db.TVShowSeasons.Add(season);
            await db.SaveChangesAsync(ct);
            db.TVShowEpisodes.Add(new TVShowEpisode { Name = "Folge", TVShowSeasonId = season.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = 1 });
            await db.SaveChangesAsync(ct);
        }

        await Task.Delay(1000, ct);
        Assert.Equal(1, await EpisodeEntryCountAsync(playlistId)); // marked, but not yet delivered
        Assert.NotEmpty(await GetMarkersAsync());

        scan.Dispose(); // end of the scan

        Assert.True(await WaitUntilAsync(async () => await EpisodeEntryCountAsync(playlistId) == 2));
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Start_ProcessesMarkersLeftFromBeforeTheLastShutdown_WithoutAnySignal()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, playlistId) = await CreateShowPlaylistAsync();
        await AddSeasonToShowAsync(showId, "Staffel 2", 1); // _db has no signal wired: only the marker remains
        Assert.NotEmpty(await GetMarkersAsync());

        using var worker = CreateWorker(Settings(sweepHours: 0));
        await worker.StartAsync(ct);

        Assert.True(await WaitUntilAsync(async () => await EpisodeEntryCountAsync(playlistId) == 2));
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenIdle_TheWorkerDoesNotPoll()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateShowPlaylistAsync();
        // The safety sweep ran just now, so it is not due for another 24 hours (a single timer, no ticking).
        var coordinatorSettings = Settings(sweepHours: 24);
        await _db.Setups.AddAsync(new Setup { PlaylistBackfillLastSweepAt = DateTime.UtcNow }, ct);
        await _db.SaveChangesAsync(ct);

        using var worker = CreateWorker(coordinatorSettings);
        await worker.StartAsync(ct);
        await WaitUntilIdleAsync();

        var quiet = _recorder.Commands.Count;
        await Task.Delay(2000, ct);

        Assert.Equal(quiet, _recorder.Commands.Count);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OverdueSafetySweep_IsCaughtUpAfterStart_ButNotRepeatedByTheNextStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, playlistId) = await CreateShowPlaylistAsync();
        var (_, newEpisodeIds) = await AddSeasonToShowAsync(showId, "Staffel 2", 1);
        await ClearMarkersAsync(); // the mark is lost: only the sweep can deliver this

        using (var firstStart = CreateWorker(Settings(sweepHours: 24)))
        {
            await firstStart.StartAsync(ct);
            Assert.True(await WaitUntilAsync(async () => await EpisodeEntryCountAsync(playlistId) == 2));
            Assert.True(await WaitUntilAsync(async () => (await _db.Setups.AsNoTracking().FirstAsync(ct)).PlaylistBackfillLastSweepAt is not null));
            await firstStart.StopAsync(CancellationToken.None);
        }

        // Another child appears with a lost mark; a restart right after must not run the sweep again.
        var (_, laterEpisodeIds) = await AddSeasonToShowAsync(showId, "Staffel 3", 1);
        await ClearMarkersAsync();
        using var secondStart = CreateWorker(Settings(sweepHours: 24));
        _recorder.Clear();
        await secondStart.StartAsync(ct);
        await WaitUntilIdleAsync();

        Assert.Equal(2, await EpisodeEntryCountAsync(playlistId));
        Assert.DoesNotContain(await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).Select(e => e.MediaId).ToListAsync(ct), id => id == laterEpisodeIds[0]);
        await secondStart.StopAsync(CancellationToken.None);
        Assert.Contains(newEpisodeIds[0], await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).Select(e => e.MediaId).ToListAsync(ct));
    }

    [Fact]
    public async Task Stop_EndsTheWorkerPromptly()
    {
        var ct = TestContext.Current.CancellationToken;
        using var worker = CreateWorker(Settings(sweepHours: 24));
        await worker.StartAsync(ct);
        await WaitUntilIdleAsync();

        var stop = worker.StopAsync(CancellationToken.None);

        Assert.Same(stop, await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(5), ct)));
    }
}
