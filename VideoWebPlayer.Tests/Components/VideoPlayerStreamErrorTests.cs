using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Shared.Media;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// A 403 or 404 on the video stream itself reaches only the browser, not the client library: the
/// <c>&lt;video&gt;</c> element raises an error event. Without a handler the player stayed empty and
/// silent — exactly the case <c>MediaAccessErrorMessages.NotFound</c> is worded for (A4).
/// </summary>
public class VideoPlayerStreamErrorTests
{
    [Fact]
    public async Task StreamError_ShowsMessageInsteadOfStayingSilent()
    {
        using var ctx = CreateTestContext();
        var cut = RenderPlayer(ctx, "/api/items/movie/7/stream");

        Assert.Empty(cut.FindAll("#player-stream-error"));

        var video = cut.Find("#video-player-element");
        await cut.InvokeAsync(() => video.TriggerEventAsync("onerror", new EventArgs()));

        var errorBox = Assert.Single(cut.FindAll("#player-stream-error"));
        Assert.Equal("Dieser Titel existiert nicht oder hat keine Videodatei.", errorBox.TextContent);
    }

    [Fact]
    public async Task StreamError_IsClearedWhenAnotherTitleIsLoaded()
    {
        using var ctx = CreateTestContext();
        var cut = RenderPlayer(ctx, "/api/items/movie/7/stream");

        var video = cut.Find("#video-player-element");
        await cut.InvokeAsync(() => video.TriggerEventAsync("onerror", new EventArgs()));
        Assert.Single(cut.FindAll("#player-stream-error"));

        cut.Render(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.StreamUrl, "/api/items/movie/8/stream")
            .Add(p => p.MediaType, "movie")
            .Add(p => p.MediaId, 8L));

        Assert.Empty(cut.FindAll("#player-stream-error"));
    }

    private static IRenderedComponent<VideoPlayer> RenderPlayer(BunitContext ctx, string streamUrl)
        => ctx.Render<VideoPlayer>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.StreamUrl, streamUrl)
            .Add(p => p.MediaType, "movie")
            .Add(p => p.MediaId, 7L));

    /// <summary>
    /// Builds a bUnit context with the dependencies <see cref="VideoPlayer"/> injects. JS interop runs in
    /// loose mode because the component sets up a real <c>&lt;video&gt;</c> element there, which has no
    /// browser to run against here.
    /// </summary>
    /// <returns>The configured test context.</returns>
    private static BunitContext CreateTestContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(new Mock<IPlaylistApiClient>().Object);
        ctx.Services.AddSingleton<ILogger<VideoPlayer>>(NullLogger<VideoPlayer>.Instance);
        return ctx;
    }
}
