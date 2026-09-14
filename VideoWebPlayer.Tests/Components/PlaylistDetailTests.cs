using System.Linq;
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
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters
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
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        Assert.Empty(cut.FindAll("#playlist-detail-error"));
        Assert.Empty(cut.FindAll("#playlist-playback-error"));
        Assert.NotEmpty(cut.FindAll("#video-player-element"));
    }

    /// <summary>
    /// Regression test for the "playback always resumes at 0:00 in a playlist context" bug (Weiterschauen
    /// mit Playlist-Bezug, Schritt 6 Nachbesserung, Problem 4): <see cref="PlaylistDetail"/> must pass
    /// <see cref="DtoPlaylistPlaybackStart.StartPositionSeconds"/> through to <see cref="VideoPlayer"/>'s
    /// <see cref="VideoPlayer.StartPositionSeconds"/> parameter instead of leaving it unset.
    /// </summary>
    [Fact]
    public void StartPlaybackAsync_OnSuccess_PassesStartPositionSecondsToVideoPlayer()
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
                MediaId = 7,
                StartPositionSeconds = 600
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=42");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(600d, videoPlayer.Instance.StartPositionSeconds);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 3": after
    /// auto-advance at media end, the new entry's own <see cref="DtoPlaylistNavigationResult.StartPositionSeconds"/>
    /// (carried directly on the navigation result, resolved server-side together with the entry itself)
    /// must be applied - and no separate <see cref="IPlaylistApiClient.StartPlaylistAsync"/> roundtrip may
    /// be issued to fetch it, since that roundtrip's intervening <c>await</c> was exactly what let Blazor
    /// render an intermediate frame with the new <c>StreamUrl</c> but the previous entry's stale position
    /// (Runde 3 finding, see <c>blocked.md</c> of this development step).
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_AutoAdvance_AppliesStartPositionFromNavigationResult()
    {
        var playlistClientMock = CreatePlaylistClientMockWithPerEntryPositions(1, new Dictionary<long, long>
        {
            [100] = 300,
            [200] = 450
        });
        playlistClientMock
            .Setup(c => c.AdvancePlaylistAsync(1, 100))
            .ReturnsAsync(new DtoPlaylistNavigationResult
            {
                Entry = new DtoPlaylistEntry { Id = 200, PlaylistId = 1, MediaType = "Movie", MediaId = 20 },
                Position = 2,
                StartPositionSeconds = 450
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=100");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var video = cut.Find("#video-player-element");
        await cut.InvokeAsync(() => video.TriggerEventAsync("onended", new EventArgs()));

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(450d, videoPlayer.Instance.StartPositionSeconds);
        playlistClientMock.Verify(c => c.StartPlaylistAsync(1, 200), Times.Never);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 3": after a
    /// manual "Nächster"-click, the new entry's own start position (carried on the navigation result) must
    /// be applied without a separate fetch - see
    /// <see cref="OnPlaylistEntryIdChanged_AutoAdvance_AppliesStartPositionFromNavigationResult"/>.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_NextButton_AppliesStartPositionFromNavigationResult()
    {
        var playlistClientMock = CreatePlaylistClientMockWithPerEntryPositions(1, new Dictionary<long, long>
        {
            [100] = 300,
            [200] = 450
        });
        playlistClientMock
            .Setup(c => c.GetNextPlaylistEntryAsync(1, 100))
            .ReturnsAsync(new DtoPlaylistNavigationResult
            {
                Entry = new DtoPlaylistEntry { Id = 200, PlaylistId = 1, MediaType = "Movie", MediaId = 20 },
                Position = 2,
                StartPositionSeconds = 450
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=100");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var nextButton = cut.Find(".playlist-next-button");
        await cut.InvokeAsync(() => nextButton.Click());

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(450d, videoPlayer.Instance.StartPositionSeconds);
        playlistClientMock.Verify(c => c.StartPlaylistAsync(1, 200), Times.Never);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 3": after a
    /// manual "Vorheriger"-click, the new entry's own start position (carried on the navigation result)
    /// must be applied - not the position of the entry navigated away from. See
    /// <see cref="OnPlaylistEntryIdChanged_AutoAdvance_AppliesStartPositionFromNavigationResult"/>.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_PreviousButton_AppliesStartPositionFromNavigationResult()
    {
        var playlistClientMock = CreatePlaylistClientMockWithPerEntryPositions(1, new Dictionary<long, long>
        {
            [100] = 300,
            [200] = 450
        });
        playlistClientMock
            .Setup(c => c.GetPreviousPlaylistEntryAsync(1, 200))
            .ReturnsAsync(new DtoPlaylistNavigationResult
            {
                Entry = new DtoPlaylistEntry { Id = 100, PlaylistId = 1, MediaType = "Movie", MediaId = 10 },
                Position = 1,
                StartPositionSeconds = 300
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=200");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var previousButton = cut.Find(".playlist-previous-button");
        await cut.InvokeAsync(() => previousButton.Click());

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(300d, videoPlayer.Instance.StartPositionSeconds);
        playlistClientMock.Verify(c => c.StartPlaylistAsync(1, 100), Times.Never);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 3": after
    /// clicking "Neu starten" at the end of the playlist, the resolved entry's own start position - already
    /// carried on the <see cref="IPlaylistApiClient.StartPlaylistAsync"/> response <see cref="VideoPlayer"/>
    /// itself makes for the internal <c>entryId: null</c> resolve - must be applied directly, without any
    /// further fetch by <see cref="PlaylistDetail"/>.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_RestartButton_AppliesReturnedStartPositionSeconds()
    {
        var playlistClientMock = CreatePlaylistClientMock();
        playlistClientMock
            .Setup(c => c.StartPlaylistAsync(1, 200))
            .ReturnsAsync(new DtoPlaylistPlaybackStart
            {
                PlaylistId = 1,
                PlaylistName = "Test-Playlist",
                TotalCount = 2,
                CurrentPosition = 2,
                CurrentEntryId = 200,
                CurrentEntry = new DtoPlaylistEntry { Id = 200, PlaylistId = 1, MediaType = "Movie", MediaId = 20 },
                StreamUrl = "/api/items/movie/20/stream",
                MediaType = "movie",
                MediaId = 20,
                StartPositionSeconds = 450
            });
        playlistClientMock
            .Setup(c => c.GetNextPlaylistEntryAsync(1, 200))
            .ReturnsAsync((DtoPlaylistNavigationResult?)null);
        playlistClientMock
            .Setup(c => c.StartPlaylistAsync(1, null))
            .ReturnsAsync(new DtoPlaylistPlaybackStart
            {
                PlaylistId = 1,
                PlaylistName = "Test-Playlist",
                TotalCount = 2,
                CurrentPosition = 1,
                CurrentEntryId = 100,
                CurrentEntry = new DtoPlaylistEntry { Id = 100, PlaylistId = 1, MediaType = "Movie", MediaId = 10 },
                StreamUrl = "/api/items/movie/10/stream",
                MediaType = "movie",
                MediaId = 10,
                StartPositionSeconds = 300
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=200");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var nextButton = cut.Find(".playlist-next-button");
        await cut.InvokeAsync(() => nextButton.Click());

        var restartButton = cut.Find(".playlist-restart-button");
        await cut.InvokeAsync(() => restartButton.Click());

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(300d, videoPlayer.Instance.StartPositionSeconds);
        Assert.Equal(10, videoPlayer.Instance.MediaId);
        playlistClientMock.Verify(c => c.StartPlaylistAsync(1, 100), Times.Never);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 3" (see
    /// <c>blocked.md</c>/<c>acceptance-schritt-6.2.md</c> of this development step): a delayed navigation
    /// response used to let Blazor Server render an intermediate frame in which <see cref="VideoPlayer"/>
    /// already carried the new entry's <c>StreamUrl</c>/<c>MediaId</c> but still the previous entry's
    /// <c>StartPositionSeconds</c> - which <see cref="VideoPlayer.OnAfterRenderAsync"/> then applied to the
    /// actual <c>&lt;video&gt;</c> element via the <c>videoPlayer.setStartPosition</c> JS call and never
    /// corrected afterwards (its <c>_startApplied</c> guard was already set). Verified here the way the
    /// acceptance check that found this verified it: with a genuinely delayed (not synchronously-completed)
    /// navigation response, asserting on the actual JS interop call - not just the eventually-consistent
    /// <c>StartPositionSeconds</c> parameter, which does not by itself prove what was applied to the player.
    /// </summary>
    [Fact]
    public async Task OnMediaEnd_DelayedNavigationResponse_JSAppliesNewEntrysStartPositionNotStaleOne()
    {
        var playlistClientMock = CreatePlaylistClientMockWithPerEntryPositions(1, new Dictionary<long, long>
        {
            [100] = 300
        });
        var navigationResponse = new TaskCompletionSource<DtoPlaylistNavigationResult?>();
        playlistClientMock.Setup(c => c.AdvancePlaylistAsync(1, 100)).Returns(navigationResponse.Task);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=100");
        var cut = ctx.Render<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var video = cut.Find("#video-player-element");
        var endTask = cut.InvokeAsync(() => video.TriggerEventAsync("onended", new EventArgs()));

        // Arrives only now, after the event handler has already suspended at this await - reproducing the
        // async gap the Runde-3 bug relied on.
        navigationResponse.SetResult(new DtoPlaylistNavigationResult
        {
            Entry = new DtoPlaylistEntry { Id = 200, PlaylistId = 1, MediaType = "Movie", MediaId = 20 },
            Position = 2,
            StartPositionSeconds = 450
        });
        await endTask;

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(20, videoPlayer.Instance.MediaId);
        Assert.Equal(450d, videoPlayer.Instance.StartPositionSeconds);

        // Two calls are expected: the initial render applies entry 100's own position (300), and the
        // auto-advance applies entry 200's own position (450). The Runde-3 bug would have made this second
        // call apply the stale, previous entry's position (300) instead - or never correct it at all.
        var setStartPositionCalls = ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "videoPlayer.setStartPosition")
            .ToList();
        Assert.Equal(2, setStartPositionCalls.Count);
        Assert.Equal(450d, setStartPositionCalls[^1].Arguments[1]);
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
    /// Builds an <see cref="IPlaylistApiClient"/> mock (see <see cref="CreatePlaylistClientMock"/>)
    /// additionally preconfigured so <see cref="IPlaylistApiClient.StartPlaylistAsync"/> returns a distinct
    /// <see cref="DtoPlaylistPlaybackStart.StartPositionSeconds"/> per entry id in
    /// <paramref name="startPositionSecondsByEntryId"/> (media id <c>entryId * 10</c>), so tests can verify
    /// that a navigation to a given entry applies that entry's own position rather than another entry's.
    /// </summary>
    /// <param name="playlistId">Id of the playlist the entries belong to.</param>
    /// <param name="startPositionSecondsByEntryId">Maps each entry id to the start position it resolves to.</param>
    /// <returns>The preconfigured mock.</returns>
    private static Mock<IPlaylistApiClient> CreatePlaylistClientMockWithPerEntryPositions(long playlistId, IReadOnlyDictionary<long, long> startPositionSecondsByEntryId)
    {
        var playlistClientMock = CreatePlaylistClientMock();
        foreach (var (entryId, startPositionSeconds) in startPositionSecondsByEntryId)
        {
            var mediaId = entryId * 10;
            playlistClientMock
                .Setup(c => c.StartPlaylistAsync(playlistId, entryId))
                .ReturnsAsync(new DtoPlaylistPlaybackStart
                {
                    PlaylistId = playlistId,
                    PlaylistName = "Test-Playlist",
                    TotalCount = startPositionSecondsByEntryId.Count,
                    CurrentPosition = 1,
                    CurrentEntryId = entryId,
                    CurrentEntry = new DtoPlaylistEntry { Id = entryId, PlaylistId = playlistId, MediaType = "Movie", MediaId = mediaId },
                    StreamUrl = $"/api/items/movie/{mediaId}/stream",
                    MediaType = "movie",
                    MediaId = mediaId,
                    StartPositionSeconds = startPositionSeconds
                });
        }
        return playlistClientMock;
    }

    /// <summary>
    /// Builds a bUnit <see cref="global::Bunit.TestContext"/> with every dependency
    /// <see cref="PlaylistDetail"/> (and its child <see cref="PlaylistEntriesList"/>/<see cref="MediaSearchSelector"/>)
    /// injects, wired to <paramref name="playlistClientMock"/>. Shared by every test in this class so the
    /// DI-registration boilerplate is written once.
    /// </summary>
    /// <param name="playlistClientMock">The <see cref="IPlaylistApiClient"/> mock to register.</param>
    /// <param name="playlistDetailLogger">
    /// The <see cref="ILogger{PlaylistDetail}"/> to register, or <see langword="null"/> (default) to
    /// register <see cref="NullLogger{PlaylistDetail}"/> - a real logger is only needed by tests that must
    /// verify an error was logged (see <see cref="CapturingLogger{T}"/>).
    /// </param>
    /// <returns>The configured test context.</returns>
    private static global::Bunit.TestContext CreateTestContext(Mock<IPlaylistApiClient> playlistClientMock, ILogger<PlaylistDetail>? playlistDetailLogger = null)
    {
        var ctx = new global::Bunit.TestContext();
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton(playlistDetailLogger ?? NullLogger<PlaylistDetail>.Instance);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);
        return ctx;
    }
}
