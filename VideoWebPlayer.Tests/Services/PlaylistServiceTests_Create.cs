using VideoWebPlayer.Events;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.CreatePlaylistAsync"/>.
/// </summary>
public class PlaylistServiceTests_Create : PlaylistServiceTestBase
{
    [Fact]
    public async Task CreatePlaylist_ValidInput_ReturnsPlaylistDto()
    {
        var ct = TestContext.Current.CancellationToken;

        var dto = await _service.CreatePlaylistAsync(_testUserId, "Meine Playlist", "Eine Beschreibung", "Manual", ct);

        Assert.True(dto.Id > 0);
        Assert.Equal("Meine Playlist", dto.Name);
        Assert.Equal("Eine Beschreibung", dto.Description);
        Assert.Equal("Manual", dto.SortMode);
    }

    [Fact]
    public async Task CreatePlaylist_EmptyName_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreatePlaylistAsync(_testUserId, "   ", null, null, ct));

        Assert.Equal("Playlist-Name ist erforderlich.", ex.Message);
    }

    [Fact]
    public async Task CreatePlaylist_NameTooLong_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var tooLongName = new string('a', 256);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreatePlaylistAsync(_testUserId, tooLongName, null, null, ct));

        Assert.Equal("Name darf maximal 255 Zeichen lang sein.", ex.Message);
    }

    [Fact]
    public async Task CreatePlaylist_DescriptionTooLong_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var tooLongDescription = new string('b', 2001);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreatePlaylistAsync(_testUserId, "Playlist", tooLongDescription, null, ct));

        Assert.Equal("Beschreibung darf maximal 2000 Zeichen lang sein.", ex.Message);
    }

    [Fact]
    public async Task CreatePlaylist_DuplicateName_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        await _service.CreatePlaylistAsync(_testUserId, "Serien-Marathon", null, null, ct);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreatePlaylistAsync(_testUserId, "serien-marathon", null, null, ct));

        Assert.Equal("Ein Playlist mit diesem Namen existiert bereits.", ex.Message);
    }

    [Fact]
    public async Task CreatePlaylist_MaxPlaylistsExceeded_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var limitedService = CreateService(maxPlaylistsPerUser: 1);

        await limitedService.CreatePlaylistAsync(_testUserId, "Erste Playlist", null, null, ct);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => limitedService.CreatePlaylistAsync(_testUserId, "Zweite Playlist", null, null, ct));

        Assert.Equal("Die maximale Anzahl an Playlists wurde erreicht.", ex.Message);
    }

    [Fact]
    public async Task CreatePlaylist_PublishesPlaylistCreatedEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        PlaylistCreatedEvent? publishedEvent = null;
        _eventManager.Subscribe<PlaylistCreatedEvent>(e => publishedEvent = e);

        var dto = await _service.CreatePlaylistAsync(_testUserId, "Event-Playlist", null, null, ct);

        Assert.NotNull(publishedEvent);
        Assert.Equal(dto.Id, publishedEvent!.Playlist.Id);
    }
}
