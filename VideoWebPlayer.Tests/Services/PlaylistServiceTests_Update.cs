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
    public async Task UpdatePlaylist_ValidInput_UpdatesNameAndDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Alter Name", "Alte Beschreibung", "ByReleaseDate", ct);

        var updated = await _service.UpdatePlaylistAsync(created.Id, _testUserId, "Neuer Name", "Neue Beschreibung", "Manual", ct);

        Assert.Equal("Neuer Name", updated.Name);
        Assert.Equal("Neue Beschreibung", updated.Description);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    /// <summary>
    /// Documents the fix for the previously reported bug where <c>UpdatePlaylistAsync</c> let the
    /// SortMode field bypass <c>ChangeSortModeAsync</c>'s confirmation/initialization logic (a plain
    /// name/description edit could silently switch a playlist to Manual mode without initializing
    /// SortOrder, or drop the manual order when switching away from it without confirmation). The
    /// sortMode parameter is now ignored entirely by UpdatePlaylistAsync; only ChangeSortModeAsync may
    /// change the sort mode.
    /// </summary>
    [Fact]
    public async Task UpdatePlaylist_SortModeParameterProvided_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, "ByReleaseDate", ct);

        var updated = await _service.UpdatePlaylistAsync(created.Id, _testUserId, "Playlist", null, "Manual", ct);

        Assert.Equal("ByReleaseDate", updated.SortMode);
    }

    [Fact]
    public async Task UpdatePlaylist_DuplicateName_ThrowsPlaylistNameAlreadyExistsException()
    {
        var ct = TestContext.Current.CancellationToken;
        await _service.CreatePlaylistAsync(_testUserId, "Playlist Eins", null, null, ct);
        var second = await _service.CreatePlaylistAsync(_testUserId, "Playlist Zwei", null, null, ct);

        var ex = await Assert.ThrowsAsync<PlaylistNameAlreadyExistsException>(
            () => _service.UpdatePlaylistAsync(second.Id, _testUserId, "playlist eins", null, null, ct));

        Assert.Equal("Ein Playlist mit diesem Namen existiert bereits.", ex.Message);
    }

    [Fact]
    public async Task UpdatePlaylist_SameNameAsBefore_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreatePlaylistAsync(_testUserId, "Unverändert", "Alt", null, ct);

        var updated = await _service.UpdatePlaylistAsync(created.Id, _testUserId, "Unverändert", "Neu", null, ct);

        Assert.Equal("Unverändert", updated.Name);
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
