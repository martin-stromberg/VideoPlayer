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
/// Entwicklungsschritt 11: the separate overview of public playlists (<see cref="PublicPlaylistsList"/>) and
/// the discreet "oeffentlich" marker in the overview of the user's own playlists (<see cref="PlaylistsList"/>).
/// </summary>
public class PublicPlaylistsListTests
{
    [Fact]
    public void PublicOverview_ShowsPublicPlaylistsAsTilesLinkingToTheDetailView()
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPublicPlaylistsAsync(null)).ReturnsAsync(new[]
        {
            new DtoPlaylist { Id = 7, Name = "Für alle", IsPublic = true, SortMode = PlaylistSortModeValues.ByReleaseDate, CoverPictureId = 5 },
            new DtoPlaylist { Id = 8, Name = "Noch eine", IsPublic = true, SortMode = PlaylistSortModeValues.Manual }
        });
        using var ctx = CreateContext(mock);

        var cut = ctx.Render<PublicPlaylistsList>();

        var tiles = cut.FindAll("a.playlist-row");
        Assert.Equal(2, tiles.Count);
        Assert.Equal("/playlists/7", tiles[0].GetAttribute("href"));
        Assert.Equal("Für alle", tiles[0].GetAttribute("data-playlist-name"));
        Assert.Contains("/api/playlists/7/cover", cut.Find(".playlist-card-cover-image").GetAttribute("src"));
    }

    [Fact]
    public void PublicOverview_OffersNoCreationOrEditing_AndShowsNoBadgeOrOwnerInformation()
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPublicPlaylistsAsync(null)).ReturnsAsync(new[]
        {
            new DtoPlaylist { Id = 7, Name = "Für alle", IsPublic = true, IsOwner = false }
        });
        using var ctx = CreateContext(mock);

        var cut = RenderCompleted(ctx);

        Assert.Empty(cut.FindAll("#create-playlist-button"));
        Assert.Empty(cut.FindComponents<PlaylistForm>());
        Assert.Empty(cut.FindAll(".playlist-public-badge"));
        Assert.DoesNotContain("Besitzer", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicOverview_NoPublicPlaylists_ShowsEmptyState()
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPublicPlaylistsAsync(null)).ReturnsAsync(Array.Empty<DtoPlaylist>());
        using var ctx = CreateContext(mock);

        var cut = ctx.Render<PublicPlaylistsList>();

        Assert.Single(cut.FindAll("#public-playlists-empty"));
        Assert.Empty(cut.FindAll("a.playlist-row"));
    }

    [Fact]
    public void PublicOverview_LoadFailure_ShowsError()
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPublicPlaylistsAsync(null)).ThrowsAsync(new HttpRequestException("boom"));
        using var ctx = CreateContext(mock);

        var cut = ctx.Render<PublicPlaylistsList>();

        Assert.Single(cut.FindAll("#public-playlists-load-error"));
    }

    [Fact]
    public void OwnOverview_MarksOwnPublicPlaylists_AndLinksToPublicOverview()
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPlaylistsAsync(null)).ReturnsAsync(new[]
        {
            new DtoPlaylist { Id = 1, Name = "Privat", IsOwner = true, IsPublic = false },
            new DtoPlaylist { Id = 2, Name = "Öffentlich", IsOwner = true, IsPublic = true }
        });
        using var ctx = CreateContext(mock);

        var cut = ctx.Render<PlaylistsList>();

        var privateTile = cut.Find("a.playlist-row[data-playlist-name='Privat']");
        var publicTile = cut.Find("a.playlist-row[data-playlist-name='Öffentlich']");
        Assert.Empty(privateTile.QuerySelectorAll(".playlist-public-badge"));
        Assert.Single(publicTile.QuerySelectorAll(".playlist-public-badge"));
        Assert.Equal("/playlists/public", cut.Find("#public-playlists-link").GetAttribute("href"));
    }

    private static IRenderedComponent<PublicPlaylistsList> RenderCompleted(global::Bunit.BunitContext ctx)
        => ctx.Render<PublicPlaylistsList>();

    private static global::Bunit.BunitContext CreateContext(Mock<IPlaylistApiClient> playlistClientMock)
    {
        var ctx = new global::Bunit.BunitContext();
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        return ctx;
    }
}
