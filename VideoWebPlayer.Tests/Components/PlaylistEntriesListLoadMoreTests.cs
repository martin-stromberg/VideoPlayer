using Bunit;
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
/// Tests for the sentinel/IntersectionObserver based page-wise loading of <see cref="PlaylistEntriesList"/>
/// (Nachbesserungsrunde 1, Entwicklungsschritt 10, Abnahme-Abweichung 3). The browser's IntersectionObserver
/// itself cannot run in bUnit; the tests drive <see cref="PlaylistEntriesList.OnBottomVisible"/> - the
/// callback the observer invokes - directly and count the <c>observeBottom</c>/<c>disconnectMediaSourceBottomObserver</c>
/// JS interop calls, which is what decides whether a failed load can turn into an endless retry loop
/// (every <c>observeBottom</c> call creates a new observer that immediately reports a still-visible sentinel).
/// </summary>
public class PlaylistEntriesListLoadMoreTests
{
    /// <summary>
    /// Regression test: after a failed next-page load the observer must NOT be re-armed (previously
    /// <c>observeBottom</c> was called again right after the error, and the fresh observer re-triggered
    /// the failing request in an endless loop), and a further observer callback must not re-request the
    /// failed page - the error stays visible together with an explicit "Erneut versuchen" button.
    /// </summary>
    [Fact]
    public async Task LoadNextPageFails_DoesNotRearmObserverAndDoesNotRetryOnItsOwn()
    {
        var (cut, clientMock, ctx) = Render(page2: () => throw new HttpRequestException("Server nicht erreichbar"));
        using var _ = ctx;
        Assert.Equal(1, ObserveBottomCalls(ctx));

        await cut.InvokeAsync(() => cut.Instance.OnBottomVisible());

        Assert.Contains("Fehler beim Nachladen der Eintraege", cut.Find("#playlist-entries-status").TextContent);
        Assert.Single(cut.FindAll("#playlist-entries-retry-button"));
        Assert.Equal(1, ObserveBottomCalls(ctx));
        VerifyPageRequests(clientMock, 2, Times.Once());

        // A spurious observer callback while the error is shown must not fire the request again.
        await cut.InvokeAsync(() => cut.Instance.OnBottomVisible());
        await cut.InvokeAsync(() => cut.Instance.OnBottomVisible());

        VerifyPageRequests(clientMock, 2, Times.Once());
        Assert.Equal(1, ObserveBottomCalls(ctx));
    }

    [Fact]
    public async Task RetryButton_AfterFailure_LoadsFailedPageAndClearsError()
    {
        var attempts = 0;
        var (cut, clientMock, ctx) = Render(page2: () =>
        {
            attempts++;
            if (attempts == 1)
                throw new HttpRequestException("Server nicht erreichbar");
            return new DtoPlaylistEntriesPagedResult { Entries = CreateEntries(100, 3), HasNextPage = false, TotalCount = 23 };
        });
        using var _ = ctx;
        await cut.InvokeAsync(() => cut.Instance.OnBottomVisible());
        Assert.Single(cut.FindAll("#playlist-entries-retry-button"));

        await cut.InvokeAsync(() => cut.Find("#playlist-entries-retry-button").Click());

        VerifyPageRequests(clientMock, 2, Times.Exactly(2));
        Assert.Empty(cut.FindAll("#playlist-entries-retry-button"));
        Assert.Empty(cut.FindAll("#playlist-entries-status"));
        Assert.Equal(23, cut.FindAll(".playlist-entry-row").Count);
    }

    /// <summary>
    /// After the last page the observer is disconnected (previously it stayed attached to the removed
    /// sentinel element), and no sentinel remains in the markup.
    /// </summary>
    [Fact]
    public async Task LastPageLoaded_DisconnectsObserverAndRemovesSentinel()
    {
        var (cut, _, ctx) = Render(page2: () => new DtoPlaylistEntriesPagedResult { Entries = CreateEntries(100, 3), HasNextPage = false, TotalCount = 23 });
        using var _ = ctx;
        Assert.Single(cut.FindAll(".playlist-entries-sentinel"));
        Assert.Equal(0, DisconnectCalls(ctx));

        await cut.InvokeAsync(() => cut.Instance.OnBottomVisible());

        Assert.Empty(cut.FindAll(".playlist-entries-sentinel"));
        Assert.Equal(1, DisconnectCalls(ctx));
    }

    [Fact]
    public async Task InitialLoadFails_ShowsRetryButtonAndDoesNotReloadOnObserverCallback()
    {
        using var ctx = new global::Bunit.BunitContext();
        var clientMock = new Mock<IPlaylistApiClient>();
        clientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(1, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Server nicht erreichbar"));
        RegisterServices(ctx, clientMock);

        var cut = ctx.Render<PlaylistEntriesList>(p => p.Add(x => x.PlaylistId, 1).Add(x => x.IsManualMode, false));
        Assert.Contains("Fehler beim Laden der Eintraege", cut.Find("#playlist-entries-status").TextContent);
        Assert.Single(cut.FindAll("#playlist-entries-retry-button"));

        await cut.InvokeAsync(() => cut.Instance.OnBottomVisible());

        VerifyPageRequests(clientMock, 1, Times.Once());
    }

    private static (IRenderedComponent<PlaylistEntriesList> Cut, Mock<IPlaylistApiClient> ClientMock, global::Bunit.BunitContext Ctx) Render(Func<DtoPlaylistEntriesPagedResult> page2)
    {
        var ctx = new global::Bunit.BunitContext();
        var clientMock = new Mock<IPlaylistApiClient>();
        clientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(1, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = CreateEntries(0, 20), HasNextPage = true, TotalCount = 23 });
        clientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(1, 2, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(page2()));
        RegisterServices(ctx, clientMock);

        var cut = ctx.Render<PlaylistEntriesList>(p => p.Add(x => x.PlaylistId, 1).Add(x => x.IsManualMode, false));
        return (cut, clientMock, ctx);
    }

    private static void RegisterServices(global::Bunit.BunitContext ctx, Mock<IPlaylistApiClient> clientMock)
    {
        ctx.JSInterop.SetupVoid("observeBottom", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("disconnectMediaSourceBottomObserver").SetVoidResult();
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(clientMock.Object);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);
    }

    private static DtoPlaylistEntry[] CreateEntries(int firstId, int count)
        => Enumerable.Range(firstId, count)
            .Select(i => new DtoPlaylistEntry
            {
                Id = i + 1,
                PlaylistId = 1,
                MediaType = "Movie",
                MediaId = 1000 + i,
                MediaTitle = $"Film {i}",
                IsAccessible = true
            })
            .ToArray();

    private static int ObserveBottomCalls(global::Bunit.BunitContext ctx)
        => ctx.JSInterop.Invocations.Count(i => i.Identifier == "observeBottom");

    private static int DisconnectCalls(global::Bunit.BunitContext ctx)
        => ctx.JSInterop.Invocations.Count(i => i.Identifier == "disconnectMediaSourceBottomObserver");

    private static void VerifyPageRequests(Mock<IPlaylistApiClient> clientMock, int page, Times times)
        => clientMock.Verify(c => c.RequestPlaylistEntriesPagedAsync(1, page, It.IsAny<int>(), It.IsAny<CancellationToken>()), times);
}
