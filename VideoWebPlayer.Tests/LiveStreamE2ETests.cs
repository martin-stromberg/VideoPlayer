using Microsoft.EntityFrameworkCore;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// HTTP-E2E tests for the live stream delivery endpoint <c>GET /api/live-stream/{fileName}</c>:
/// anonymous requests are refused, a paired device reaches the HLS playlist — rewritten so each
/// segment URI carries the <c>access_token</c> — and its segments, and invalid or missing files
/// answer 404.
/// </summary>
[Trait("Category", "E2E")]
public sealed class LiveStreamE2ETests : DeviceClientTestBase
{
    private async Task<string> SeedSegmentDirectoryAsync()
    {
        var segDir = Path.Combine(Path.GetTempPath(), $"vwp-livestream-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(segDir);
        await File.WriteAllTextAsync(Path.Combine(segDir, "live.m3u8"),
            "#EXTM3U\n#EXT-X-VERSION:3\n#EXTINF:6.0,\nseg_001.ts\n");
        await File.WriteAllTextAsync(Path.Combine(segDir, "seg_001.ts"), "segment-bytes");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var setup = await db.Setups.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        if (setup is null)
        {
            setup = new Setup();
            db.Setups.Add(setup);
        }
        setup.LiveStreamSegmentDirectory = segDir;
        setup.TvSenderEnabled = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return segDir;
    }

    [Fact]
    public async Task Anonymous_RequestPlaylist_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync();
        using var http = CreateHttpClient();

        var response = await http.GetAsync("/api/live-stream/live.m3u8", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_RequestSegment_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync();
        using var http = CreateHttpClient();

        var response = await http.GetAsync("/api/live-stream/seg_001.ts", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PairedDevice_RequestPlaylistWithAccessToken_ReturnsRewrittenPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync();
        var (_, payload) = await CreateUserAndPairDeviceAsync($"live-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/live-stream/live.m3u8?access_token={payload.Token}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var playlist = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("#EXTM3U", playlist);
        Assert.Contains($"seg_001.ts?access_token={payload.Token}", playlist);
        Assert.Equal("no-store", response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync();
        var (_, payload) = await CreateUserAndPairDeviceAsync($"live-seg-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/live-stream/seg_001.ts?access_token={payload.Token}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("video/mp2t", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("segment-bytes", await response.Content.ReadAsStringAsync(ct));
    }

    [Theory]
    [InlineData("missing.ts")]
    [InlineData("..%2Fsecret.ts")]
    [InlineData("a%20b.ts")]
    public async Task PairedDevice_RequestInvalidOrMissing_Returns404(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync();
        var (_, payload) = await CreateUserAndPairDeviceAsync($"live-404-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/live-stream/{fileName}?access_token={payload.Token}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
