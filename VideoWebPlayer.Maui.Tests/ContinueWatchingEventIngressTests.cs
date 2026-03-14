using VideoWebPlayer.Services;

namespace VideoWebPlayer.Maui.Tests;

public class ContinueWatchingEventIngressTests
{
    [Fact]
    public async Task EnqueueOrUpdate_SingleEvent_CanBeRead()
    {
        var buffer = new ContinueWatchingBuffer();

        buffer.EnqueueOrUpdate("user-1", 10, null, TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(90));

        var entry = await buffer.ReadNextAsync(CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal("user-1", entry.UserId);
        Assert.Equal(10, entry.MovieId);
        Assert.Null(entry.EpisodeId);
        Assert.Equal(TimeSpan.FromSeconds(15), entry.Position);
    }

    [Fact]
    public async Task EnqueueOrUpdate_SameKeyTwice_ReturnsLatestThenNullForDuplicateQueueKey()
    {
        var buffer = new ContinueWatchingBuffer();

        buffer.EnqueueOrUpdate("user-1", 10, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(90));
        buffer.EnqueueOrUpdate("user-1", 10, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(90));

        var first = await buffer.ReadNextAsync(CancellationToken.None);
        var second = await buffer.ReadNextAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal(TimeSpan.FromSeconds(30), first.Position);
        Assert.Null(second);
    }

    [Fact]
    public async Task EnqueueOrUpdate_DifferentKeys_EventsAreReadSeparately()
    {
        var buffer = new ContinueWatchingBuffer();

        buffer.EnqueueOrUpdate("user-1", 10, null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(80));
        buffer.EnqueueOrUpdate("user-1", null, 77, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(40));

        var first = await buffer.ReadNextAsync(CancellationToken.None);
        var second = await buffer.ReadNextAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);

        var entries = new[] { first, second };
        Assert.Contains(entries, e => e!.MovieId == 10 && e.EpisodeId is null);
        Assert.Contains(entries, e => e!.MovieId is null && e.EpisodeId == 77);
    }
}
