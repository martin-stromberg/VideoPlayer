using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for <see cref="LiveStreamController"/>: the <c>.m3u8</c> rewrite injecting the
/// <c>access_token</c> into segment URIs, content types and the 404 paths for invalid,
/// traversing and missing files.
/// </summary>
public sealed class LiveStreamControllerTests : IAsyncLifetime
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"vwp-lsctrl-{Guid.NewGuid():N}");
    private string _segDir = null!;
    private LiveStreamController _controller = null!;
    private ProgramScheduleTestDb _fixture = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _fixture = await ProgramScheduleTestDb.CreateAsync($"lsctrl-{Guid.NewGuid():N}", ct);
        var settings = new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance);
        var store = new LiveStreamSegmentStore(
            new TestWebHostEnvironment { ContentRootPath = _contentRoot },
            settings, NullLogger<LiveStreamSegmentStore>.Instance);
        _segDir = await store.GetDirectoryAsync(ct);

        _controller = new LiveStreamController(store,
            new FakeAuthService { CurrentUser = new ApplicationUser { Id = "u1", UserName = "u@x.de" } },
            NullLogger<LiveStreamController>.Instance);
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    public async ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
        await Task.CompletedTask;
    }

    private void SetQueryToken(string? token)
    {
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["access_token"] = token ?? string.Empty
        });
        _controller.HttpContext.Request.Query = query;
    }

    [Fact]
    public async Task GetFile_M3u8_InjectsAccessTokenIntoSegmentUris()
    {
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_segDir, "live.m3u8"),
            "#EXTM3U\n#EXT-X-VERSION:3\n#EXTINF:6.0,\nseg_001.ts\n#EXTINF:6.0,\nseg_002.ts\n", ct);
        SetQueryToken("tok 123/+=");

        var result = await _controller.GetFile("live.m3u8");

        var content = Assert.IsType<ContentResult>(result);
        Assert.StartsWith("application/vnd.apple.mpegurl", content.ContentType);
        Assert.Contains("seg_001.ts?access_token=tok%20123%2F%2B%3D", content.Content);
        Assert.Contains("seg_002.ts?access_token=tok%20123%2F%2B%3D", content.Content);
        Assert.Contains("#EXTM3U", content.Content);
        Assert.DoesNotContain("EXTINF:6.0,?access_token", content.Content);
        Assert.Equal("no-store", _controller.HttpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetFile_M3u8WithoutToken_LeavesUrisUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_segDir, "live.m3u8"), "#EXTM3U\nseg_001.ts\n", ct);
        SetQueryToken(null);

        var result = await _controller.GetFile("live.m3u8");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Contains("seg_001.ts", content.Content);
        Assert.DoesNotContain("access_token", content.Content);
    }

    [Fact]
    public async Task GetFile_TsSegment_ReturnsVideoMp2t()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(_segDir, "seg_001.ts");
        await File.WriteAllTextAsync(path, "segment-bytes", ct);

        var result = await _controller.GetFile("seg_001.ts");

        var file = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("video/mp2t", file.ContentType);
    }

    [Theory]
    [InlineData("../evil.ts")]
    [InlineData("sub/evil.ts")]
    [InlineData("a b.ts")]
    [InlineData("not-there.ts")]
    [InlineData("live.m3u8")]
    public async Task GetFile_InvalidOrMissing_ReturnsNotFound(string fileName)
    {
        var result = await _controller.GetFile(fileName);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetFile_NotLoggedIn_Returns401()
    {
        var controller = new LiveStreamController(
            new LiveStreamSegmentStore(
                new TestWebHostEnvironment { ContentRootPath = _contentRoot },
                new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
                NullLogger<LiveStreamSegmentStore>.Instance),
            new FakeAuthService(), NullLogger<LiveStreamController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.GetFile("live.m3u8");

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
