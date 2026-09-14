using System.Collections.Concurrent;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for <see cref="VideoWebPlayer.Components.Playlists.MediaSearchSelector"/>: the debounced
/// live-search input, the HTTP call it issues via <see cref="VideoWebPlayerClient"/> and the
/// <c>OnMediaSelected</c> callback fired when a search result is clicked.
/// </summary>
public class MediaSearchSelectorTests
{
    private static FakeVideoWebPlayerClient CreateFakeClient(params MediaEntryDto[] results)
        => new() { ResultsToReturn = results.ToList() };

    [Fact]
    public async Task SearchTermInput_DebounceWorks_RespectsDelay()
    {
        using var ctx = new global::Bunit.BunitContext();
        var fakeClient = CreateFakeClient(new MediaEntryDto { Type = "Movie", Id = 1, Title = "Breaking Point" });
        ctx.Services.AddSingleton<VideoWebPlayerClient>(fakeClient);
        ctx.Services.AddSingleton<ILogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>>(NullLogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>.Instance);

        var cut = ctx.Render<VideoWebPlayer.Components.Playlists.MediaSearchSelector>();
        var input = cut.Find("input.media-search-input");

        await cut.InvokeAsync(() => input.Input("B"));
        await cut.InvokeAsync(() => input.Input("Br"));
        await cut.InvokeAsync(() => input.Input("Breaking"));

        // Rapid successive input changes must not each trigger their own HTTP call; only the last
        // one, after the debounce delay elapses, should.
        Assert.Equal(0, fakeClient.CallCount);

        cut.WaitForAssertion(() => Assert.Equal(1, fakeClient.CallCount), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task HttpCall_ItemsEndpoint_ReceivesCorrectUrl()
    {
        using var ctx = new global::Bunit.BunitContext();
        var fakeClient = CreateFakeClient(new MediaEntryDto { Type = "Movie", Id = 1, Title = "Breaking Point" });
        ctx.Services.AddSingleton<VideoWebPlayerClient>(fakeClient);
        ctx.Services.AddSingleton<ILogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>>(NullLogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>.Instance);

        var cut = ctx.Render<VideoWebPlayer.Components.Playlists.MediaSearchSelector>();
        var input = cut.Find("input.media-search-input");

        await cut.InvokeAsync(() => input.Input("Breaking"));

        cut.WaitForAssertion(() => Assert.Contains(fakeClient.RequestedUrls, u => u.Contains("/api/items") && u.Contains("search=Breaking")), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task EventCallback_OnMediaSelected_InvokedWithCorrectParameters()
    {
        using var ctx = new global::Bunit.BunitContext();
        var fakeClient = CreateFakeClient(new MediaEntryDto { Type = "Movie", Id = 42, Title = "Breaking Point" });
        ctx.Services.AddSingleton<VideoWebPlayerClient>(fakeClient);
        ctx.Services.AddSingleton<ILogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>>(NullLogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>.Instance);

        (string MediaType, long MediaId)? received = null;
        var cut = ctx.Render<VideoWebPlayer.Components.Playlists.MediaSearchSelector>(parameters => parameters
            .Add(p => p.OnMediaSelected, EventCallback.Factory.Create<(string MediaType, long MediaId)>(this, args => received = args)));

        var input = cut.Find("input.media-search-input");
        await cut.InvokeAsync(() => input.Input("Breaking"));

        cut.WaitForState(() => cut.FindAll(".media-search-result").Count > 0, TimeSpan.FromSeconds(3));

        var result = cut.Find(".media-search-result");
        await cut.InvokeAsync(() => result.Click());

        Assert.NotNull(received);
        Assert.Equal("Movie", received!.Value.MediaType);
        Assert.Equal(42, received.Value.MediaId);
    }

    [Fact]
    public async Task Search_NoResults_ShowsEmptyMessage()
    {
        using var ctx = new global::Bunit.BunitContext();
        var fakeClient = CreateFakeClient();
        ctx.Services.AddSingleton<VideoWebPlayerClient>(fakeClient);
        ctx.Services.AddSingleton<ILogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>>(NullLogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>.Instance);

        var cut = ctx.Render<VideoWebPlayer.Components.Playlists.MediaSearchSelector>();
        var input = cut.Find("input.media-search-input");

        await cut.InvokeAsync(() => input.Input("Unbekannt"));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".media-search-empty")), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Search_HttpRequestFails_LogsAndShowsDistinctErrorState()
    {
        using var ctx = new global::Bunit.BunitContext();
        var fakeClient = new ThrowingVideoWebPlayerClient();
        ctx.Services.AddSingleton<VideoWebPlayerClient>(fakeClient);
        var loggedMessages = new ConcurrentQueue<string>();
        ctx.Services.AddSingleton<ILogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>>(
            new ListLogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>(loggedMessages));

        var cut = ctx.Render<VideoWebPlayer.Components.Playlists.MediaSearchSelector>();
        var input = cut.Find("input.media-search-input");

        await cut.InvokeAsync(() => input.Input("Breaking"));

        // A real request failure must be surfaced as its own error state - distinguishable from the
        // "no results" case - and logged for diagnostics, instead of being silently swallowed and shown
        // as an empty result set.
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".media-search-error")), TimeSpan.FromSeconds(3));
        Assert.Empty(cut.FindAll(".media-search-empty"));
        Assert.Contains(loggedMessages, m => m.Contains("Mediensuche"));
    }

    [Fact]
    public async Task Dispose_CancelsPendingDebouncedSearch_WithoutThrowing()
    {
        using var ctx = new global::Bunit.BunitContext();
        var fakeClient = CreateFakeClient(new MediaEntryDto { Type = "Movie", Id = 1, Title = "Breaking Point" });
        ctx.Services.AddSingleton<VideoWebPlayerClient>(fakeClient);
        ctx.Services.AddSingleton<ILogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>>(NullLogger<VideoWebPlayer.Components.Playlists.MediaSearchSelector>.Instance);

        var cut = ctx.Render<VideoWebPlayer.Components.Playlists.MediaSearchSelector>();
        var input = cut.Find("input.media-search-input");

        await cut.InvokeAsync(() => input.Input("Breaking"));

        // Dispose while the debounce delay is still pending: must not throw, and the debounced search
        // scheduled before disposal must not fire afterwards.
        var exception = await Record.ExceptionAsync(() => ctx.DisposeComponentsAsync());
        Assert.Null(exception);

        await Task.Delay(TimeSpan.FromMilliseconds(600), global::Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(0, fakeClient.CallCount);
    }

    /// <summary>
    /// <see cref="VideoWebPlayerClient"/> test double whose HTTP helper always throws, for verifying that
    /// a real search failure is logged and surfaced as an error state distinct from "no results".
    /// </summary>
    private sealed class ThrowingVideoWebPlayerClient : VideoWebPlayerClient
    {
        public ThrowingVideoWebPlayerClient() : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
        {
        }

        protected override Task<T> HttpGetAsync<T>(string endPoint, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulierter Serverfehler");
    }

    /// <summary>
    /// <see cref="VideoWebPlayerClient"/> test double that overrides the protected HTTP helper to
    /// return canned results without performing any real HTTP call, and records every requested URL
    /// and call count for the debounce/URL assertions above.
    /// </summary>
    private sealed class FakeVideoWebPlayerClient : VideoWebPlayerClient
    {
        public List<string> RequestedUrls { get; } = new();
        public List<MediaEntryDto> ResultsToReturn { get; set; } = new();
        public int CallCount { get; private set; }

        public FakeVideoWebPlayerClient() : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
        {
        }

        protected override Task<T> HttpGetAsync<T>(string endPoint, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(endPoint);
            CallCount++;

            if (ResultsToReturn is T typedResult)
                return Task.FromResult(typedResult);

            throw new NotSupportedException($"Unerwarteter Rueckgabetyp {typeof(T)} in FakeVideoWebPlayerClient.");
        }
    }
}
