using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.UpdatePlaylistAsync"/>.
/// </summary>
public class PlaylistServiceTests_Update : PlaylistServiceTestBase
{
    [Fact]
    public async Task UpdatePlaylist_ValidInput_UpdatesEntity()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Alter Name", "Alte Beschreibung", "ByReleaseDate", ct);

        var updated = await _service.UpdatePlaylistAsync(created.Id, _testUserId, "Neuer Name", "Neue Beschreibung", "Manual", ct);

        Assert.Equal("Neuer Name", updated.Name);
        Assert.Equal("Neue Beschreibung", updated.Description);
        Assert.Equal("Manual", updated.SortMode);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task UpdatePlaylist_DuplicateName_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        await _service.CreatePlaylistAsync(_testUserId, "Playlist Eins", null, null, ct);
        var second = await _service.CreatePlaylistAsync(_testUserId, "Playlist Zwei", null, null, ct);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdatePlaylistAsync(second.Id, _testUserId, "playlist eins", null, null, ct));

        Assert.Equal("Ein Playlist mit diesem Namen existiert bereits.", ex.Message);
    }

    [Fact]
    public async Task UpdatePlaylist_SameNameAsBefore_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Unveraendert", "Alt", null, ct);

        var updated = await _service.UpdatePlaylistAsync(created.Id, _testUserId, "Unveraendert", "Neu", null, ct);

        Assert.Equal("Unveraendert", updated.Name);
        Assert.Equal("Neu", updated.Description);
    }

    [Fact]
    public async Task UpdatePlaylist_OwnershipViolation_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Meine Playlist", null, null, ct);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.UpdatePlaylistAsync(created.Id, _otherUserId, "Neuer Name", null, null, ct));
    }

    [Fact]
    public async Task UpdatePlaylist_NotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdatePlaylistAsync(999999, _testUserId, "Neuer Name", null, null, ct));
    }
}
