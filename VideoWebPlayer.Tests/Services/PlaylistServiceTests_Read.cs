using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.GetPlaylistsAsync"/> and
/// <see cref="VideoWebPlayer.Services.PlaylistService.GetPlaylistAsync"/>.
/// </summary>
public class PlaylistServiceTests_Read : PlaylistServiceTestBase
{
    [Fact]
    public async Task GetPlaylists_ReturnsUserPlaylists()
    {
        var ct = TestContext.Current.CancellationToken;
        await _service.CreatePlaylistAsync(_testUserId, "Playlist A", null, null, ct);
        await _service.CreatePlaylistAsync(_testUserId, "Playlist B", null, null, ct);
        await _service.CreatePlaylistAsync(_otherUserId, "Fremde Playlist", null, null, ct);

        var result = await _service.GetPlaylistsAsync(_testUserId, cancellationToken: ct);

        Assert.Equal(2, result.Length);
        Assert.All(result, dto => Assert.DoesNotContain("Fremde", dto.Name));
    }

    [Fact]
    public async Task GetPlaylists_EmptyList_ReturnsEmptyArray()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _service.GetPlaylistsAsync(_testUserId, cancellationToken: ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlaylist_ValidId_ReturnsPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Meine Playlist", null, null, ct);

        var result = await _service.GetPlaylistAsync(created.Id, _testUserId, ct);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
        Assert.Equal("Meine Playlist", result.Name);
    }

    [Fact]
    public async Task GetPlaylist_InvalidId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _service.GetPlaylistAsync(999999, _testUserId, ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlaylist_OwnershipViolation_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Meine Playlist", null, null, ct);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.GetPlaylistAsync(created.Id, _otherUserId, ct));
    }
}
