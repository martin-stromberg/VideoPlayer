using System.Reflection;
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
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(600d, videoPlayer.Instance.StartPositionSeconds);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 2": after
    /// auto-advance at media end, <see cref="PlaylistDetail.OnPlaylistEntryIdChangedAsync"/> must fetch and
    /// apply the new entry's own <see cref="DtoPlaylistPlaybackStart.StartPositionSeconds"/> via
    /// <see cref="IPlaylistApiClient.StartPlaylistAsync"/> instead of leaving the previous entry's stale
    /// position in place.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_AutoAdvance_FetchesAndAppliesNewStartPositionSeconds()
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
                Position = 2
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=100");
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var video = cut.Find("#video-player-element");
        await cut.InvokeAsync(() => video.TriggerEventAsync("onended", new EventArgs()));

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(450d, videoPlayer.Instance.StartPositionSeconds);
        playlistClientMock.Verify(c => c.StartPlaylistAsync(1, 200), Times.Once);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 2": after a
    /// manual "Nächster"-click, the new entry's own start position must be fetched and applied - see
    /// <see cref="OnPlaylistEntryIdChanged_AutoAdvance_FetchesAndAppliesNewStartPositionSeconds"/>.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_NextButton_FetchesAndAppliesNewStartPositionSeconds()
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
                Position = 2
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=100");
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var nextButton = cut.Find(".playlist-next-button");
        await cut.InvokeAsync(() => nextButton.Click());

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(450d, videoPlayer.Instance.StartPositionSeconds);
        playlistClientMock.Verify(c => c.StartPlaylistAsync(1, 200), Times.Once);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 2": after a
    /// manual "Vorheriger"-click, the new entry's own start position must be fetched and applied - not the
    /// position of the entry navigated away from. See
    /// <see cref="OnPlaylistEntryIdChanged_AutoAdvance_FetchesAndAppliesNewStartPositionSeconds"/>.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_PreviousButton_FetchesAndAppliesNewStartPositionSeconds()
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
                Position = 1
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=200");
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var previousButton = cut.Find(".playlist-previous-button");
        await cut.InvokeAsync(() => previousButton.Click());

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(300d, videoPlayer.Instance.StartPositionSeconds);
        playlistClientMock.Verify(c => c.StartPlaylistAsync(1, 100), Times.Once);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 2": after
    /// clicking "Neu starten" at the end of the playlist, the resulting entry's own start position must be
    /// fetched and applied - not the stale position of the entry playback ended on, and not whatever
    /// <see cref="IPlaylistApiClient.StartPlaylistAsync"/> happened to return for the internal
    /// <c>entryId: null</c> resolve call <see cref="VideoPlayer"/> makes first.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_RestartButton_FetchesAndAppliesNewStartPositionSeconds()
    {
        var playlistClientMock = CreatePlaylistClientMockWithPerEntryPositions(1, new Dictionary<long, long>
        {
            [100] = 300,
            [200] = 450
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
                StartPositionSeconds = 999
            });

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=200");
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var nextButton = cut.Find(".playlist-next-button");
        await cut.InvokeAsync(() => nextButton.Click());

        var restartButton = cut.Find(".playlist-restart-button");
        await cut.InvokeAsync(() => restartButton.Click());

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        // The genuine per-entry position (300, fetched by PlaylistDetail for entry 100) must win, not the
        // stale position of the entry playback ended on (450) and not the decoy value (999) the internal
        // entryId:null resolve call returned.
        Assert.Equal(300d, videoPlayer.Instance.StartPositionSeconds);
        playlistClientMock.Verify(c => c.StartPlaylistAsync(1, 100), Times.Once);
    }

    /// <summary>
    /// Regression test for "Nachbesserung Weiterschauen mit Playlist-Bezug, Schritt 6, Runde 2": if the
    /// additional <see cref="IPlaylistApiClient.StartPlaylistAsync"/> call
    /// <see cref="PlaylistDetail.OnPlaylistEntryIdChangedAsync"/> makes to fetch the new entry's start
    /// position fails, the error must be logged and <c>playbackStart.StartPositionSeconds</c> must fall
    /// back to <c>0</c> (start from the beginning) instead of propagating the exception or keeping the
    /// stale position - while the other fields (<c>MediaId</c>, <c>MediaType</c>, <c>StreamUrl</c>) are
    /// still updated to the new entry.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_StartPlaylistAsync_ThrowsException_FallsBackToZeroPosition()
    {
        var playlistClientMock = CreatePlaylistClientMock();
        playlistClientMock
            .Setup(c => c.StartPlaylistAsync(1, 100))
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
        playlistClientMock
            .Setup(c => c.AdvancePlaylistAsync(1, 100))
            .ReturnsAsync(new DtoPlaylistNavigationResult
            {
                Entry = new DtoPlaylistEntry { Id = 200, PlaylistId = 1, MediaType = "Movie", MediaId = 20 },
                Position = 2
            });
        playlistClientMock
            .Setup(c => c.StartPlaylistAsync(1, 200))
            .ThrowsAsync(new HttpRequestException("Serverfehler", null, System.Net.HttpStatusCode.InternalServerError));

        var logger = new CapturingLogger<PlaylistDetail>();
        using var ctx = CreateTestContext(playlistClientMock, logger);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=100");
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        var video = cut.Find("#video-player-element");
        await cut.InvokeAsync(() => video.TriggerEventAsync("onended", new EventArgs()));

        var videoPlayer = cut.FindComponent<VideoPlayer>();
        Assert.Equal(0d, videoPlayer.Instance.StartPositionSeconds);
        Assert.Equal(20, videoPlayer.Instance.MediaId);
        Assert.Equal("movie", videoPlayer.Instance.MediaType);
        Assert.True(logger.ErrorLogged);
    }

    /// <summary>
    /// Regression test for the <c>CurrentEntryId</c> race-condition guard in
    /// <see cref="PlaylistDetail.OnPlaylistEntryIdChangedAsync"/> (see <c>review-code.1.md</c>, Befund 2):
    /// if two calls overlap - e.g. rapid successive entry changes - and the additional
    /// <see cref="IPlaylistApiClient.StartPlaylistAsync"/> response for the entry navigated away from
    /// arrives after the response for the entry actually navigated to, the stale, out-of-order response
    /// must not overwrite the already-more-recent <c>playbackStart</c> state.
    /// </summary>
    [Fact]
    public async Task OnPlaylistEntryIdChanged_OverlappingCalls_StaleResponseDoesNotOverwriteNewerState()
    {
        var playlistClientMock = CreatePlaylistClientMock();
        playlistClientMock
            .Setup(c => c.StartPlaylistAsync(1, 100))
            .ReturnsAsync(new DtoPlaylistPlaybackStart
            {
                PlaylistId = 1,
                PlaylistName = "Test-Playlist",
                TotalCount = 3,
                CurrentPosition = 1,
                CurrentEntryId = 100,
                CurrentEntry = new DtoPlaylistEntry { Id = 100, PlaylistId = 1, MediaType = "Movie", MediaId = 10 },
                StreamUrl = "/api/items/movie/10/stream",
                MediaType = "movie",
                MediaId = 10,
                StartPositionSeconds = 5
            });

        var staleResponse = new TaskCompletionSource<DtoPlaylistPlaybackStart>();
        var freshResponse = new TaskCompletionSource<DtoPlaylistPlaybackStart>();
        playlistClientMock.Setup(c => c.StartPlaylistAsync(1, 200)).Returns(staleResponse.Task);
        playlistClientMock.Setup(c => c.StartPlaylistAsync(1, 300)).Returns(freshResponse.Task);

        using var ctx = CreateTestContext(playlistClientMock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=100");
        var cut = ctx.RenderComponent<PlaylistDetail>(parameters => parameters
            .Add(p => p.Id, 1));

        // Simulates two rapid, overlapping entry changes: the request for entry 200 (navigated away from)
        // is started first but its response is delayed; the request for entry 300 (the entry actually
        // navigated to) is started while the first one is still in flight. Invoking the private
        // OnPlaylistEntryIdChangedAsync method directly (rather than via VideoPlayer's EventCallback, whose
        // ComponentBase.IHandleEvent wrapper calls StateHasChanged and therefore requires the bUnit
        // renderer's Dispatcher) executes each call synchronously up to its first incomplete await (the
        // gated StartPlaylistAsync response), giving deterministic control over the interleaving.
        var onPlaylistEntryIdChangedAsync = typeof(PlaylistDetail).GetMethod("OnPlaylistEntryIdChangedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task InvokeOnPlaylistEntryIdChangedAsync(VideoPlayer.PlaylistEntryPlaybackInfo info)
            => (Task)onPlaylistEntryIdChangedAsync.Invoke(cut.Instance, new object[] { info })!;

        var staleCallTask = InvokeOnPlaylistEntryIdChangedAsync(new VideoPlayer.PlaylistEntryPlaybackInfo(200, "movie", 20, "/api/items/movie/20/stream"));
        var freshCallTask = InvokeOnPlaylistEntryIdChangedAsync(new VideoPlayer.PlaylistEntryPlaybackInfo(300, "movie", 30, "/api/items/movie/30/stream"));

        // The response for the currently displayed entry (300) arrives first and is applied.
        freshResponse.SetResult(new DtoPlaylistPlaybackStart
        {
            PlaylistId = 1,
            PlaylistName = "Test-Playlist",
            TotalCount = 3,
            CurrentPosition = 3,
            CurrentEntryId = 300,
            CurrentEntry = new DtoPlaylistEntry { Id = 300, PlaylistId = 1, MediaType = "Movie", MediaId = 30 },
            StreamUrl = "/api/items/movie/30/stream",
            MediaType = "movie",
            MediaId = 30,
            StartPositionSeconds = 700
        });
        await freshCallTask;

        // The stale response for the entry navigated away from (200) arrives afterwards and must be discarded.
        staleResponse.SetResult(new DtoPlaylistPlaybackStart
        {
            PlaylistId = 1,
            PlaylistName = "Test-Playlist",
            TotalCount = 3,
            CurrentPosition = 2,
            CurrentEntryId = 200,
            CurrentEntry = new DtoPlaylistEntry { Id = 200, PlaylistId = 1, MediaType = "Movie", MediaId = 20 },
            StreamUrl = "/api/items/movie/20/stream",
            MediaType = "movie",
            MediaId = 20,
            StartPositionSeconds = 999
        });
        await staleCallTask;

        var playbackStartField = typeof(PlaylistDetail).GetField("playbackStart", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var playbackStart = Assert.IsType<DtoPlaylistPlaybackStart>(playbackStartField.GetValue(cut.Instance));

        Assert.Equal(300, playbackStart.CurrentEntryId);
        Assert.Equal(700, playbackStart.StartPositionSeconds);

        // The stale call for entry 200 must not navigate the URL back after the fresh call for entry 300
        // already navigated forward - otherwise a reload would resume at the wrong (stale) entry (Af-010).
        var currentUri = ctx.Services.GetRequiredService<NavigationManager>().Uri;
        Assert.Contains("entryId=300", currentUri);
        Assert.DoesNotContain("entryId=200", currentUri);
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
        ctx.AddTestAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton(playlistDetailLogger ?? NullLogger<PlaylistDetail>.Instance);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);
        return ctx;
    }
}
