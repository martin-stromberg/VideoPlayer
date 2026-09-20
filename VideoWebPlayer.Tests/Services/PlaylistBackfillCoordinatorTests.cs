using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="PlaylistBackfillCoordinator"/>: marker-driven work in bounded blocks with pauses, and
/// the persisted once-a-day safety sweep.
/// </summary>
public class PlaylistBackfillCoordinatorTests : PlaylistServiceTestBase, IDisposable
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly List<TimeSpan> _pauses = new();
    private readonly List<IDisposable> _disposables = new();

    public new void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
        base.Dispose();
    }

    private PlaylistBackfillCoordinator CreateCoordinator(PlaylistSettings settings, IBackgroundProcessingGate? gate = null)
    {
        var provider = PlaylistBackfillTestServices.Build(_connectionString, settings, null, gate);
        _disposables.Add(provider);
        return new PlaylistBackfillCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(settings),
            NullLogger<PlaylistBackfillCoordinator>.Instance,
            _time,
            (delay, _) =>
            {
                _pauses.Add(delay);
                return Task.CompletedTask;
            });
    }

    // Creates count playlists, each with its own show and a later new season; returns the show ids.
    private async Task<List<long>> CreatePlaylistsWithNewSeasonAsync(int count)
    {
        var showIds = new List<long>();
        for (var i = 0; i < count; i++)
        {
            var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync($"Serie {i}", "Staffel 1", 1);
            await CreateTestPlaylistWithEntriesAsync(_testUserId,
                (MediaTypeValues.TVShow, showId),
                (MediaTypeValues.TVShowSeason, seasonId),
                (MediaTypeValues.TVShowEpisode, episodeIds[0]));
            showIds.Add(showId);
        }

        await ClearMarkersAsync();
        foreach (var showId in showIds)
            await AddSeasonToShowAsync(showId, "Staffel 2", 1);
        return showIds;
    }

    private static PlaylistSettings Settings(int batchSize = 2, int sweepHours = 24, int pauseSeconds = 3)
        => new() { BackfillBatchSize = batchSize, BackfillSafetySweepIntervalHours = sweepHours, BackfillBlockPauseSeconds = pauseSeconds };

    [Fact]
    public async Task ProcessPending_WorksInBlocksWithPausesBetweenThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreatePlaylistsWithNewSeasonAsync(5);
        var coordinator = CreateCoordinator(Settings(batchSize: 2, pauseSeconds: 3));

        var summary = await coordinator.ProcessPendingAsync(ct);

        Assert.Equal(5, summary.PlaylistsExamined);
        Assert.Equal(10, summary.EntriesAdded); // per playlist: new season + its episode
        Assert.Equal(0, summary.PlaylistsFailed);
        Assert.Equal(new[] { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3) }, _pauses); // 3 blocks -> 2 pauses
        Assert.Empty(await GetMarkersAsync());
    }

    [Fact]
    public async Task ProcessPending_NothingMarked_DoesNothingAndDoesNotPause()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreatePlaylistsWithNewSeasonAsync(3);
        await ClearMarkersAsync();
        var coordinator = CreateCoordinator(Settings());

        var summary = await coordinator.ProcessPendingAsync(ct);

        Assert.Equal(default, summary);
        Assert.Empty(_pauses);
    }

    [Fact]
    public async Task ProcessPending_EntersTheBackgroundGateForEveryWorkUnit()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreatePlaylistsWithNewSeasonAsync(3);
        var gate = new CountingGate();
        var coordinator = CreateCoordinator(Settings(batchSize: 2), gate);

        await coordinator.ProcessPendingAsync(ct);

        // plan + 2 blocks + release
        Assert.Equal(4, gate.Entered);
        Assert.Equal(4, gate.Released);
    }

    [Fact]
    public async Task SafetySweep_NeverRan_IsDueAndRunsInBlocks_WithoutTouchingMarkers()
    {
        var ct = TestContext.Current.CancellationToken;
        var showIds = await CreatePlaylistsWithNewSeasonAsync(5);
        // The marks are lost (as if a path had bypassed the hook), except a marker that must survive the sweep.
        await ClearMarkersAsync();
        _db.PlaylistBackfillMarkers.Add(new PlaylistBackfillMarker { MediaType = MediaTypeValues.TVShow, MediaId = showIds[0] });
        await _db.SaveChangesAsync(ct);
        var coordinator = CreateCoordinator(Settings(batchSize: 2, pauseSeconds: 3));

        Assert.True(await coordinator.RunSafetySweepIfDueAsync(ct));

        Assert.Equal(2, _pauses.Count);
        Assert.Equal(5 + 5, await _db.PlaylistEntries.AsNoTracking().CountAsync(e => e.MediaType == MediaTypeValues.TVShowEpisode, ct)); // 5 old + 5 new
        Assert.Single(await GetMarkersAsync());
        Assert.NotNull((await _db.Setups.AsNoTracking().FirstAsync(ct)).PlaylistBackfillLastSweepAt);
    }

    [Fact]
    public async Task SafetySweep_JustRan_IsNotDue_AlsoNotAfterARestart()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreatePlaylistsWithNewSeasonAsync(1);
        var settings = Settings();

        Assert.True(await CreateCoordinator(settings).RunSafetySweepIfDueAsync(ct));

        _time.Advance(TimeSpan.FromHours(23));
        var afterRestart = CreateCoordinator(settings); // a new instance = a restart; only the database remembers
        Assert.False(await afterRestart.RunSafetySweepIfDueAsync(ct));
        Assert.Equal(_time.GetUtcNow().AddHours(-23).AddHours(24).UtcDateTime, (await afterRestart.GetSafetySweepDueAtAsync(ct))!.Value.UtcDateTime);
    }

    [Fact]
    public async Task SafetySweep_DueAgainAfterTheInterval_AlsoAfterARestart()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreatePlaylistsWithNewSeasonAsync(1);
        var settings = Settings();
        await CreateCoordinator(settings).RunSafetySweepIfDueAsync(ct);

        _time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));

        Assert.True(await CreateCoordinator(settings).RunSafetySweepIfDueAsync(ct));
    }

    [Fact]
    public async Task SafetySweep_IntervalZero_IsSwitchedOff()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreatePlaylistsWithNewSeasonAsync(1);
        var coordinator = CreateCoordinator(Settings(sweepHours: 0));

        Assert.Null(await coordinator.GetSafetySweepDueAtAsync(ct));
        Assert.False(await coordinator.RunSafetySweepIfDueAsync(ct));
        Assert.Null((await _db.Setups.AsNoTracking().FirstOrDefaultAsync(ct))?.PlaylistBackfillLastSweepAt);
    }

    [Fact]
    public async Task SafetySweep_FailingPlaylist_DoesNotStopTheSweep()
    {
        var ct = TestContext.Current.CancellationToken;
        var showIds = await CreatePlaylistsWithNewSeasonAsync(3);
        await ClearMarkersAsync();
        var failingPlaylistId = await _db.PlaylistEntries.AsNoTracking()
            .Where(e => e.MediaType == MediaTypeValues.TVShow && e.MediaId == showIds[0]).Select(e => e.PlaylistId).SingleAsync(ct);
        await _db.Database.ExecuteSqlRawAsync(
            $"CREATE TRIGGER fail_playlist BEFORE INSERT ON \"PlaylistEntries\" WHEN NEW.\"PlaylistId\" = {failingPlaylistId} BEGIN SELECT RAISE(ABORT, 'boom'); END;");
        var coordinator = CreateCoordinator(Settings(batchSize: 10));

        var summary = await coordinator.RunSafetySweepAsync(ct);

        Assert.Equal(1, summary.PlaylistsFailed);
        Assert.Equal(4, summary.EntriesAdded); // the two healthy playlists still got season + episode
    }

    private sealed class CountingGate : IBackgroundProcessingGate
    {
        public int Entered { get; private set; }

        public int Released { get; private set; }

        public bool IsPausedForRestore => false;

        public int ActiveOperationCount => Entered - Released;

        public Task<IAsyncDisposable> EnterOperationAsync(string name, CancellationToken cancellationToken)
        {
            Entered++;
            return Task.FromResult<IAsyncDisposable>(new Lease(this));
        }

        public Task<IAsyncDisposable> PauseForRestoreAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        private sealed class Lease : IAsyncDisposable
        {
            private readonly CountingGate _owner;

            public Lease(CountingGate owner) => _owner = owner;

            public ValueTask DisposeAsync()
            {
                _owner.Released++;
                return ValueTask.CompletedTask;
            }
        }
    }
}
