using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests für das Erstellen/Aktualisieren von <see cref="ContinueWatchingEntry"/> mit Playlist-Bezug.
/// </summary>
public sealed class ContinueWatchingServicePlaylistTests : ContinueWatchingServiceTestBase
{
    [Fact]
    public async Task Playlist_CreateEntry_WithSamePlaylistId_UpdatesExisting()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        var playlist = await CreateTestPlaylistAsync(_testUserId, "Playlist 1");

        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(5), Duration, playlist.Id, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(10), Duration, playlist.Id, ct: ct);

        var entries = await _db.ContinueWatchingEntries
            .Where(x => x.UserId == _testUserId && x.MovieId == movie.Id && x.PlaylistId == playlist.Id)
            .ToListAsync(ct);

        var entry = Assert.Single(entries);
        Assert.Equal(TimeSpan.FromMinutes(10), entry.Position);
    }

    [Fact]
    public async Task Playlist_CreateEntry_WithDifferentPlaylistIds_AllowsBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        var playlist1 = await CreateTestPlaylistAsync(_testUserId, "Playlist 1");
        var playlist2 = await CreateTestPlaylistAsync(_testUserId, "Playlist 2");

        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(5), Duration, playlist1.Id, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(7), Duration, playlist2.Id, ct: ct);

        var entries = await _db.ContinueWatchingEntries
            .Where(x => x.UserId == _testUserId && x.MovieId == movie.Id)
            .ToListAsync(ct);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.PlaylistId == playlist1.Id);
        Assert.Contains(entries, e => e.PlaylistId == playlist2.Id);
    }

    [Fact]
    public async Task Playlist_CreateEntry_WithAndWithoutPlaylistId_Independent()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        var playlist = await CreateTestPlaylistAsync(_testUserId, "Playlist 1");

        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(5), Duration, ct: ct);
        await _service.ProcessBufferedEntryAsync(_testUserId, movie.Id, null, TimeSpan.FromMinutes(7), Duration, playlist.Id, ct: ct);

        var entries = await _db.ContinueWatchingEntries
            .Where(x => x.UserId == _testUserId && x.MovieId == movie.Id)
            .ToListAsync(ct);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.PlaylistId == null);
        Assert.Contains(entries, e => e.PlaylistId == playlist.Id);
    }

    [Fact]
    public async Task Playlist_ValidateOwnership_NonOwner_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await CreateTestPlaylistAsync("other-user", "Fremde Playlist");

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.ValidatePlaylistAccessAsync(_testUserId, playlist.Id, ct));
    }

    [Fact]
    public async Task Playlist_ValidateOwnership_PlaylistNotFound_Throws()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.ValidatePlaylistAccessAsync(_testUserId, 999_999, ct));
    }
}
