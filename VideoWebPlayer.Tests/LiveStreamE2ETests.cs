using Microsoft.EntityFrameworkCore;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
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
    private async Task<string> SeedSegmentDirectoryAsync(bool writePlaylist = true)
    {
        var segDir = Path.Combine(Path.GetTempPath(), $"vwp-livestream-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(segDir);
        if (writePlaylist)
        {
            await File.WriteAllTextAsync(Path.Combine(segDir, "live.m3u8"),
                "#EXTM3U\n#EXT-X-VERSION:3\n#EXTINF:6.0,\nseg_001.ts\n");
            await File.WriteAllTextAsync(Path.Combine(segDir, "seg_001.ts"), "segment-bytes");
        }

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var setup = await scope.ServiceProvider.GetRequiredService<ProgramSettingsService>().GetOrCreateSetupAsync(TestContext.Current.CancellationToken);
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
        var response = await http.GetAsync($"/api/live-stream/live.m3u8?access_token={payload.Token!}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var playlist = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("#EXTM3U", playlist);
        Assert.Contains($"seg_001.ts?access_token={payload.Token!}", playlist);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync();
        var (_, payload) = await CreateUserAndPairDeviceAsync($"live-seg-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/live-stream/seg_001.ts?access_token={payload.Token!}", ct);

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
        var response = await http.GetAsync($"/api/live-stream/{fileName}?access_token={payload.Token!}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync(writePlaylist: false);
        var (_, payload) = await CreateUserAndPairDeviceAsync($"live-standby-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/live-stream/live.m3u8?access_token={payload.Token!}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var playlist = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("#EXTM3U", playlist);
        Assert.Contains("standby_0_", playlist);
        Assert.Contains($"access_token={payload.Token!}", playlist);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task PairedDevice_RequestStandbySegment_ReturnsSegment()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync(writePlaylist: false);
        var (_, payload) = await CreateUserAndPairDeviceAsync($"live-standbyseg-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/live-stream/standby_0_00.ts?access_token={payload.Token!}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("video/mp2t", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(0x47, bytes[0]);
    }

    [Fact]
    public async Task PairedDevice_RequestPlaylist_TransitionsStandbyToReal_Discontinuity()
    {
        var ct = TestContext.Current.CancellationToken;
        var segDir = await SeedSegmentDirectoryAsync(writePlaylist: false);
        var (_, payload) = await CreateUserAndPairDeviceAsync($"live-trans-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        var standby = await http.GetAsync($"/api/live-stream/live.m3u8?access_token={payload.Token!}", ct);
        Assert.Equal(HttpStatusCode.OK, standby.StatusCode);
        Assert.Contains("standby_0_", await standby.Content.ReadAsStringAsync(ct));

        // Erste echte Segmentierung steht: Playlist mit mindestens einem Segment.
        await File.WriteAllTextAsync(Path.Combine(segDir, "live.m3u8"),
            "#EXTM3U\n#EXT-X-VERSION:3\n#EXTINF:6.0,\nseg_001.ts\n", ct);
        await File.WriteAllTextAsync(Path.Combine(segDir, "seg_001.ts"), "segment-bytes", ct);

        var real = await http.GetAsync($"/api/live-stream/live.m3u8?access_token={payload.Token!}", ct);
        Assert.Equal(HttpStatusCode.OK, real.StatusCode);
        var playlist = await real.Content.ReadAsStringAsync(ct);
        var discont = playlist.IndexOf("#EXT-X-DISCONTINUITY", StringComparison.Ordinal);
        Assert.True(discont >= 0 && discont < playlist.IndexOf("seg_001.ts", StringComparison.Ordinal));
        Assert.Contains($"seg_001.ts?access_token={payload.Token!}", playlist);
        Assert.Equal("no-store", real.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task PairedDevice_RequestSegment_ReturnsNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSegmentDirectoryAsync();
        var (_, payload) = await CreateUserAndPairDeviceAsync($"live-nostore-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);
        Assert.NotNull(payload.Token);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/live-stream/seg_001.ts?access_token={payload.Token!}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task PairedDevice_ConfiguredStandbyClip_PreparesAndServes()
    {
        var ct = TestContext.Current.CancellationToken;
        var ffmpeg = E2EBinaryLocator.Find("ffmpeg");
        if (ffmpeg is null)
            return; // kein ffmpeg auf diesem System

        var tempDir = Path.Combine(Path.GetTempPath(), $"vwp-standby-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var video = Path.Combine(tempDir, "standby-input.mp4");
        try
        {
            // Testvideo via lavfi (~6 s, Muster FfmpegLiveStreamSegmenterE2ETests).
            var gen = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = "-y -v error -f lavfi -i testsrc=size=320x240:rate=10 "
                    + "-f lavfi -i sine=frequency=440 -t 6 -c:v libx264 -preset ultrafast "
                    + "-pix_fmt yuv420p -c:a aac -shortest \"" + video + "\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            await gen.WaitForExitAsync(ct);
            if (!File.Exists(video))
                return;

            await SeedSegmentDirectoryAsync(writePlaylist: false);
            long itemId;
            using (var scope = CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var source = new MediaSource { Name = $"standby-src {Guid.NewGuid():N}", SourceType = MediaSourceType.LocalDirectory, Path = tempDir };
                db.MediaSources.Add(source);
                await db.SaveChangesAsync(ct);
                var collection = new MediaCollection { Name = "standby-col", MediaSourceId = source.Id, Path = tempDir, CreatedAt = DateTime.UtcNow };
                db.MediaCollections.Add(collection);
                await db.SaveChangesAsync(ct);
                var item = new MediaItem { Name = "standby-input.mp4", Path = video, MediaCollectionId = collection.Id, Duration = TimeSpan.FromSeconds(6), DurationProbedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
                db.MediaItems.Add(item);
                await db.SaveChangesAsync(ct);
                itemId = item.Id;
                var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
                await settings.UpdateLiveStreamStandbyClipAsync(itemId, ct);
            }

            var (_, payload) = await CreateUserAndPairDeviceAsync($"live-cust-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);
            using var http = CreateHttpClient();

            var playlist = string.Empty;
            for (var i = 0; i < 120 && !playlist.Contains("standby_1_"); i++)
            {
                var response = await http.GetAsync($"/api/live-stream/live.m3u8?access_token={payload.Token!}", ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                playlist = await response.Content.ReadAsStringAsync(ct);
                if (!playlist.Contains("standby_1_"))
                    await Task.Delay(500, ct);
            }

            Assert.Contains("standby_1_", playlist);

            var segmentResponse = await http.GetAsync($"/api/live-stream/standby_1_00.ts?access_token={payload.Token!}", ct);
            Assert.Equal(HttpStatusCode.OK, segmentResponse.StatusCode);
            Assert.Equal("video/mp2t", segmentResponse.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
