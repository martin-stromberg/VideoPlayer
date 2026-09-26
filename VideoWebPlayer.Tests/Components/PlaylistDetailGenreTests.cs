using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Playlists;
using VideoWebPlayer.Components.Shared.Media;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Tests for <see cref="PlaylistDetail"/>'s genre display and manual override UI (Entwicklungsschritt 9):
/// showing the derived/overridden genres, the "manually set" indicator, opening
/// <see cref="PlaylistGenreEditor"/> to override them, and the reset-to-automatic action.
/// </summary>
public class PlaylistDetailGenreTests
{
    [Fact]
    public void PlaylistDetail_WithDerivedGenres_DisplaysGenreNamesAndNoOverrideBadge()
    {
        var playlistClientMock = CreatePlaylistClientMock(new[]
        {
            new DtoGenreOption { Id = 1, Name = "Action" },
            new DtoGenreOption { Id = 2, Name = "Drama" }
        }, genresManuallyOverridden: false);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        var genresElement = Assert.Single(cut.FindAll("#playlist-detail-genres"));
        Assert.Equal("Action, Drama", genresElement.TextContent);
        Assert.Empty(cut.FindAll("#playlist-detail-genres-override-badge"));
        Assert.Empty(cut.FindAll("#playlist-detail-reset-genres-button"));
    }

    [Fact]
    public void PlaylistDetail_ManuallyOverridden_ShowsOverrideBadgeAndResetButton()
    {
        var playlistClientMock = CreatePlaylistClientMock(new[]
        {
            new DtoGenreOption { Id = 1, Name = "Handverlesen" }
        }, genresManuallyOverridden: true);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        Assert.Single(cut.FindAll("#playlist-detail-genres-override-badge"));
        Assert.Single(cut.FindAll("#playlist-detail-reset-genres-button"));
    }

    [Fact]
    public void PlaylistDetail_ClickEditGenresButton_OpensGenreEditor()
    {
        var playlistClientMock = CreatePlaylistClientMock(Array.Empty<DtoGenreOption>(), genresManuallyOverridden: false);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        Assert.Empty(cut.FindComponents<PlaylistGenreEditor>());

        cut.Find("#playlist-detail-edit-genres-button").Click();

        Assert.Single(cut.FindComponents<PlaylistGenreEditor>());
    }

    [Fact]
    public async Task PlaylistDetail_ClickResetGenresButton_ReplacesGenresWithAutomaticResult()
    {
        var playlistClientMock = CreatePlaylistClientMock(new[]
        {
            new DtoGenreOption { Id = 1, Name = "Handverlesen" }
        }, genresManuallyOverridden: true);
        playlistClientMock
            .Setup(c => c.ResetPlaylistGenresAsync(1))
            .ReturnsAsync(new DtoPlaylist
            {
                Id = 1,
                Name = "Test-Playlist",
                IsOwner = true,
                SortMode = PlaylistSortModeValues.ByReleaseDate,
                Genres = new[] { new DtoGenreOption { Id = 2, Name = "Action" } },
                GenresManuallyOverridden = false
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        await cut.InvokeAsync(() => cut.Find("#playlist-detail-reset-genres-button").Click());

        var genresElement = Assert.Single(cut.FindAll("#playlist-detail-genres"));
        Assert.Equal("Action", genresElement.TextContent);
        Assert.Empty(cut.FindAll("#playlist-detail-reset-genres-button"));
        playlistClientMock.Verify(c => c.ResetPlaylistGenresAsync(1), Times.Once);
    }

    /// <summary>
    /// Builds an <see cref="IPlaylistApiClient"/> mock preconfigured with the playlist-load and
    /// entries-page responses <see cref="PlaylistDetail"/> needs, with the given genres/override flag on
    /// the loaded playlist. Mirrors <c>PlaylistDetailTests.CreatePlaylistClientMock</c>.
    /// </summary>
    /// <param name="genres">The genres the mocked playlist load should return.</param>
    /// <param name="genresManuallyOverridden">Whether the mocked playlist load should report the genres as manually overridden.</param>
    /// <returns>The preconfigured mock.</returns>
    private static Mock<IPlaylistApiClient> CreatePlaylistClientMock(DtoGenreOption[] genres, bool genresManuallyOverridden)
    {
        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistAsync(1))
            .ReturnsAsync(new DtoPlaylist
            {
                Id = 1,
                Name = "Test-Playlist",
                IsOwner = true,
                SortMode = PlaylistSortModeValues.ByReleaseDate,
                Genres = genres,
                AllGenreIds = genres.Select(g => g.Id).ToArray(),
                GenresManuallyOverridden = genresManuallyOverridden
            });
        playlistClientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Array.Empty<DtoPlaylistEntry>(), HasNextPage = false, TotalCount = 0 });
        return playlistClientMock;
    }

    private static global::Bunit.BunitContext CreateTestContext(Mock<IPlaylistApiClient> playlistClientMock)
    {
        var ctx = new global::Bunit.BunitContext();
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton(NullLogger<PlaylistDetail>.Instance);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);
        return ctx;
    }
}
