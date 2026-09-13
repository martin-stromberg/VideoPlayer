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
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Regression tests for the "playback-start failure replaces the whole PlaylistDetail page" bug
/// (Playlist-Wiedergabe Schritt 5, Runde 1 Nachbesserung, Punkt 2/4): a failed <c>StartPlaylistAsync</c>
/// call (e.g. the requested entry is locked or a non-playable collection entry) must set the dedicated
/// <c>playbackError</c> field, not <c>loadError</c> - the latter, per <see cref="PlaylistDetail"/>'s
/// exclusive if/else-if rendering chain, replaces the entire detail page (name, entries list, actions)
/// with just an error box, even though the playlist itself loaded fine and only starting playback at this
/// particular entry failed.
/// </summary>
public class PlaylistDetailTests
{
    [Fact]
    public void StartPlaybackAsync_OnFailure_SetsPlaybackErrorNotLoadError()
    {
        var playlistClientMock = CreatePlaylistClientMock();
        playlistClientMock
            .Setup(c => c.StartPlaylistAsync(1, It.IsAny<long?>()))
            .ThrowsAsync(new HttpRequestException("Zugriff verweigert", null, System.Net.HttpStatusCode.Forbidden));

        using var ctx = CreateTestContext(playlistClientMock);

        // EntryId is [SupplyParameterFromQuery]: it must come from the current URI (mirroring an initial
        // navigation with ?entryId=999 in the browser), not a direct RenderComponent parameter.
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=999");
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        // The playlist itself loaded fine: no top-level "loadError" box replacing the whole page, the
        // name and entries list are still rendered.
        Assert.Empty(cut.FindAll("#playlist-detail-error"));
        Assert.NotEmpty(cut.FindAll("#playlist-detail-name"));

        // The playback-start failure is shown as a dedicated, inline error instead.
        var playbackErrorBox = Assert.Single(cut.FindAll("#playlist-playback-error"));
        Assert.Contains("Fehler beim Starten der Wiedergabe", playbackErrorBox.TextContent);
    }

    [Fact]
    public void StartPlaybackAsync_OnSuccess_ClearsPreviousPlaybackError()
    {
        var playlistClientMock = CreatePlaylistClientMock();
        playlistClientMock
            .Setup(c => c.StartPlaylistAsync(1, It.IsAny<long?>()))
            .ReturnsAsync(new DtoPlaylistPlaybackStart
            {
                PlaylistId = 1,
                PlaylistName = "Test-Playlist",
                TotalCount = 1,
                CurrentPosition = 1,
                CurrentEntryId = 42,
                CurrentEntry = new DtoPlaylistEntry { Id = 42, PlaylistId = 1, MediaType = "Movie", MediaId = 7 },
                StreamUrl = "/api/items/movie/7/stream",
                MediaType = "movie",
                MediaId = 7
            });

        using var ctx = CreateTestContext(playlistClientMock);
        // On success, PlaylistDetail renders VideoPlayer, whose OnAfterRenderAsync calls into
        // videoPlayer.* JS interop (video element setup) that has no real browser to run against here.
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=42");
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        Assert.Empty(cut.FindAll("#playlist-detail-error"));
        Assert.Empty(cut.FindAll("#playlist-playback-error"));
        Assert.NotEmpty(cut.FindAll("#video-player-element"));
    }

    /// <summary>
    /// Builds an <see cref="IPlaylistApiClient"/> mock preconfigured with the playlist-load and
    /// entries-page responses every test in this class needs (a single, empty "Test-Playlist"), leaving
    /// only the playback-start-specific setup (<see cref="IPlaylistApiClient.StartPlaylistAsync"/>) to
    /// each test.
    /// </summary>
    /// <returns>The preconfigured mock.</returns>
    private static Mock<IPlaylistApiClient> CreatePlaylistClientMock()
    {
        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistAsync(1))
            .ReturnsAsync(new DtoPlaylist { Id = 1, Name = "Test-Playlist", SortMode = PlaylistSortModeValues.ByReleaseDate });
        playlistClientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Array.Empty<DtoPlaylistEntry>(), HasNextPage = false, TotalCount = 0 });
        return playlistClientMock;
    }

    /// <summary>
    /// Builds a bUnit <see cref="global::Bunit.TestContext"/> with every dependency
    /// <see cref="PlaylistDetail"/> (and its child <see cref="PlaylistEntriesList"/>/<see cref="MediaSearchSelector"/>)
    /// injects, wired to <paramref name="playlistClientMock"/>. Shared by every test in this class so the
    /// DI-registration boilerplate is written once.
    /// </summary>
    /// <param name="playlistClientMock">The <see cref="IPlaylistApiClient"/> mock to register.</param>
    /// <returns>The configured test context.</returns>
    private static global::Bunit.TestContext CreateTestContext(Mock<IPlaylistApiClient> playlistClientMock)
    {
        var ctx = new global::Bunit.TestContext();
        ctx.AddTestAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton<ILogger<PlaylistDetail>>(NullLogger<PlaylistDetail>.Instance);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);
        return ctx;
    }
}
