using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Playlists;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Tests for <see cref="PlaylistsList"/>'s cover display (Entwicklungsschritt 10, Playlist-Abbildungen):
/// a playlist with a cover shows its image, one without falls back to <see cref="PlaylistCoverPlaceholder"/>.
/// </summary>
public class PlaylistsListCoverTests
{
    [Fact]
    public void PlaylistsList_WithCover_ShowsCoverImage()
    {
        var playlistClientMock = CreatePlaylistClientMock(new[]
        {
            new DtoPlaylist { Id = 1, Name = "Mit Cover", SortMode = PlaylistSortModeValues.ByReleaseDate, CoverPictureId = 10 },
            new DtoPlaylist { Id = 2, Name = "Ohne Cover", SortMode = PlaylistSortModeValues.ByReleaseDate, CoverPictureId = null }
        });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = ctx.Render<PlaylistsList>();

        var images = cut.FindAll(".playlist-card-cover-image");
        var single = Assert.Single(images);
        Assert.Contains("/api/playlists/1/cover", single.GetAttribute("src"));
    }

    [Fact]
    public void PlaylistsList_NoCover_ShowsPlaceholderOnly()
    {
        var playlistClientMock = CreatePlaylistClientMock(new[]
        {
            new DtoPlaylist { Id = 2, Name = "Ohne Cover", SortMode = PlaylistSortModeValues.ByReleaseDate, CoverPictureId = null }
        });

        using var ctx = CreateTestContext(playlistClientMock);
        var cut = ctx.Render<PlaylistsList>();

        Assert.Empty(cut.FindAll(".playlist-card-cover-image"));
        Assert.Single(cut.FindComponents<PlaylistCoverPlaceholder>());
    }

    private static Mock<IPlaylistApiClient> CreatePlaylistClientMock(DtoPlaylist[] playlists)
    {
        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistsAsync(null))
            .ReturnsAsync(playlists);
        return playlistClientMock;
    }

    private static global::Bunit.BunitContext CreateTestContext(Mock<IPlaylistApiClient> playlistClientMock)
    {
        var ctx = new global::Bunit.BunitContext();
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        return ctx;
    }
}
