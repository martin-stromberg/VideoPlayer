using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.RemoveMediaFromPlaylistAsync"/>.
/// </summary>
public class PlaylistServiceTests_RemoveMedia : PlaylistServiceTestBase
{
    [Fact]
    public async Task RemoveMedia_ValidEntry_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        await _service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == movieId, ct));
    }

    [Fact]
    public async Task RemoveMedia_NotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, 999999, ct));
    }

    [Fact]
    public async Task RemoveMedia_NotOwner_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.RemoveMediaFromPlaylistAsync(playlistId, _otherUserId, MediaTypeValues.Movie, movieId, ct));
    }
}
