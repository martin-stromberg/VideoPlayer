using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class ContinueWatchingWatchedStatusTests : ContinueWatchingServiceTestBase
{
    [Fact]
    public async Task ProcessBufferedEntry_InsideEndThreshold_MarksMovieWatched()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = 123L;

        await _service.ProcessBufferedEntryAsync(
            _testUserId,
            movieId,
            null,
            Duration - TimeSpan.FromSeconds(30),
            Duration,
            ct);

        var watched = await _db.WatchedEntries.SingleAsync(x => x.UserId == _testUserId && x.MovieId == movieId, ct);
        Assert.True(watched.WatchedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task ProcessBufferedEntry_OutsideEndThreshold_DoesNotMarkMovieWatched()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = 456L;

        await _service.ProcessBufferedEntryAsync(
            _testUserId,
            movieId,
            null,
            Duration - TimeSpan.FromSeconds(31),
            Duration,
            ct);

        Assert.Empty(await _db.WatchedEntries.Where(x => x.UserId == _testUserId && x.MovieId == movieId).ToListAsync(ct));
    }

    [Fact]
    public async Task ProcessBufferedEntry_RepeatedCompletion_UpdatesSingleWatchedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var episodeId = 789L;

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episodeId, Duration - TimeSpan.FromSeconds(10), Duration, ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, null, episodeId, Duration - TimeSpan.FromSeconds(5), Duration, ct);

        Assert.Single(await _db.WatchedEntries.Where(x => x.UserId == _testUserId && x.TVShowEpisodeId == episodeId).ToListAsync(ct));
    }
}
