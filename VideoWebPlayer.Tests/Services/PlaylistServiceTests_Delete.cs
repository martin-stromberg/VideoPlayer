using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.DeletePlaylistAsync"/>.
/// </summary>
public class PlaylistServiceTests_Delete : PlaylistServiceTestBase
{
    [Fact]
    public async Task DeletePlaylist_ValidInput_RemovesFromDb()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Zu Loeschen", null, null, ct);

        await _service.DeletePlaylistAsync(created.Id, _testUserId, ct);

        Assert.False(await _db.Playlists.AnyAsync(p => p.Id == created.Id, ct));
    }

    [Fact]
    public async Task DeletePlaylist_OwnershipViolation_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Meine Playlist", null, null, ct);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.DeletePlaylistAsync(created.Id, _otherUserId, ct));

        Assert.True(await _db.Playlists.AnyAsync(p => p.Id == created.Id, ct));
    }

    [Fact]
    public async Task DeletePlaylist_NotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.DeletePlaylistAsync(999999, _testUserId, ct));
    }
}
