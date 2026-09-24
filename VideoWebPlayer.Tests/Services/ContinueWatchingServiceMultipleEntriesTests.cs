using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests für das mehrfache Vorkommen desselben Videos in der Weiterschauen-Liste mit unterschiedlichen
/// Playlist-Bindungen (oder ohne Playlist-Bezug).
/// </summary>
public sealed class ContinueWatchingServiceMultipleEntriesTests : ContinueWatchingServiceTestBase
{
    [Fact]
    public async Task MultipleEntries_SameVideoThreePlaylists_AllExist()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        var playlist1 = await CreateTestPlaylistAsync(_testUserId, "Playlist 1");
        var playlist2 = await CreateTestPlaylistAsync(_testUserId, "Playlist 2");
        var playlist3 = await CreateTestPlaylistAsync(_testUserId, "Playlist 3");
        await CreateTestPlaylistEntryAsync(playlist1.Id, movie.Id, MediaTypeValues.Movie);
        await CreateTestPlaylistEntryAsync(playlist2.Id, movie.Id, MediaTypeValues.Movie);
        await CreateTestPlaylistEntryAsync(playlist3.Id, movie.Id, MediaTypeValues.Movie);

        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(5), Duration, playlist1.Id, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(6), Duration, playlist2.Id, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(7), Duration, playlist3.Id, ct: ct);

        var entries = await _db.ContinueWatchingEntries.Where(x => x.UserId == _testUserId && x.MovieId == movie.Id).ToListAsync(ct);
        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, e => e.PlaylistId == playlist1.Id);
        Assert.Contains(entries, e => e.PlaylistId == playlist2.Id);
        Assert.Contains(entries, e => e.PlaylistId == playlist3.Id);
    }

    [Fact]
    public async Task MultipleEntries_SameVideoWithAndWithoutPlaylist_Separate()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        var playlist1 = await CreateTestPlaylistAsync(_testUserId, "Playlist 1");
        var playlist2 = await CreateTestPlaylistAsync(_testUserId, "Playlist 2");
        await CreateTestPlaylistEntryAsync(playlist1.Id, movie.Id, MediaTypeValues.Movie);
        await CreateTestPlaylistEntryAsync(playlist2.Id, movie.Id, MediaTypeValues.Movie);

        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(5), Duration, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(6), Duration, playlist1.Id, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(7), Duration, playlist2.Id, ct: ct);

        var entries = await _db.ContinueWatchingEntries.Where(x => x.UserId == _testUserId && x.MovieId == movie.Id).ToListAsync(ct);
        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, e => e.PlaylistId == null);
        Assert.Contains(entries, e => e.PlaylistId == playlist1.Id);
        Assert.Contains(entries, e => e.PlaylistId == playlist2.Id);
    }

    [Fact]
    public async Task MultipleEntries_MarkWatched_DeletesAllVariants()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        var playlist1 = await CreateTestPlaylistAsync(_testUserId, "Playlist 1");
        var playlist2 = await CreateTestPlaylistAsync(_testUserId, "Playlist 2");
        await CreateTestPlaylistEntryAsync(playlist1.Id, movie.Id, MediaTypeValues.Movie);
        await CreateTestPlaylistEntryAsync(playlist2.Id, movie.Id, MediaTypeValues.Movie);

        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(5), Duration, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(6), Duration, playlist1.Id, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(7), Duration, playlist2.Id, ct: ct);

        Assert.Equal(3, await _db.ContinueWatchingEntries.CountAsync(x => x.UserId == _testUserId && x.MovieId == movie.Id, ct));

        // Video (innerhalb Playlist 1) als "gesehen" markieren -> ALLE Varianten muessen entfernt werden,
        // unabhaengig von ihrer PlaylistId.
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, CompletedPosition, Duration, playlist1.Id, ct: ct);

        Assert.Equal(0, await _db.ContinueWatchingEntries.CountAsync(x => x.UserId == _testUserId && x.MovieId == movie.Id, ct));
        Assert.True(await _db.WatchedEntries.AnyAsync(x => x.UserId == _testUserId && x.MovieId == movie.Id, ct));
    }
}
