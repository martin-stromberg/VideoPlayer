using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        _fixture.Db.Users.Add(new ApplicationUser { Id = "u1", UserName = "u@x.de" });
        _fixture.Db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = "u1" });
        await _fixture.Db.SaveChangesAsync(ct);

        _controller = new LiveStreamController(store, CreateStandby(), settings,
            new FakeAuthService { CurrentUser = new ApplicationUser { Id = "u1", UserName = "u@x.de" } },
            NullLogger<LiveStreamController>.Instance);
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    // Echter Provider — die eingebetteten Default-Segmente liegen in der Assembly;
    // loadDefaultSet: () => null simuliert den Degradationspfad fehlender Assets.
    private LiveStreamStandbyProvider CreateStandby(Func<StandbySegmentSet?>? loadDefaultSet = null)
        => new(new TestWebHostEnvironment { ContentRootPath = _contentRoot },
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new LiveStreamStandbySegmenter(new NoToolLocator(), new NoToolLocator(),
                NullLogger<LiveStreamStandbySegmenter>.Instance),
            NullLogger<LiveStreamStandbyProvider>.Instance, null, loadDefaultSet, null, null);

    private sealed class NoToolLocator : IFfmpegLocator, IFfprobeLocator
    {
        public string? Resolve() => null;
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

        var file = Assert.IsType<FileStreamResult>(result);
        await using var stream = file.FileStream;
        Assert.Equal("video/mp2t", file.ContentType);
    }

    [Fact]
    public async Task GetFile_TsSegment_FileCanBeDeletedWhileDeliveryStreamIsOpen()
    {
        var ct = TestContext.Current.CancellationToken;
        var expected = new byte[] { 0x47, 0x11, 0x22, 0x33, 0x44 };
        var path = Path.Combine(_segDir, "seg_001.ts");
        await File.WriteAllBytesAsync(path, expected, ct);

        var result = await _controller.GetFile("seg_001.ts");

        // (a) Plattformunabhängige Absicherung gegen einen Rückbau auf PhysicalFile:
        // Linux würde unlink auch ohne FileShare.Delete zulassen, darum trägt dort allein
        // diese Typ-Assertion den Share-Flag-Nachweis. (b) und (c) sind reine
        // Windows-Diskriminatoren — .NET emuliert FileShare unter Unix nur binär
        // (flock LOCK_SH/LOCK_EX) und FileShare.Delete ist dort gar nicht beobachtbar
        // (File.Delete ist ein blankes unlink, das keine Locks konsultiert).
        var file = Assert.IsType<FileStreamResult>(result);
        await using var delivery = file.FileStream;
        Assert.IsType<FileStream>(delivery);

        // (b) Windows-Diskriminator ReadWrite-Share: ein zweites Handle mit
        // FileAccess.ReadWrite öffnet sich nur, wenn der Auslieferungs-Stream
        // FileShare.ReadWrite gewährt — mit dem alten FileShare.Read-Handle
        // (PhysicalFile-Pfad) scheitert dieser Zweitgriff unter Windows.
        using (var second = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            Assert.True(second.CanWrite);
        }

        // (c) Windows-Diskriminator Delete-Share: ohne FileShare.Delete schlägt
        // File.Delete unter Windows mit einer IOException fehl, solange der
        // Auslieferungs-Stream offen ist (Fehlverhalten-Reproduktion).
        var deleteError = Record.Exception(() => File.Delete(path));
        Assert.Null(deleteError);

        // (d) Der Auslieferungs-Stream liefert nach dem Löschen noch den
        // vollständigen Inhalt.
        using var delivered = new MemoryStream();
        await delivery.CopyToAsync(delivered, ct);
        Assert.Equal(expected, delivered.ToArray());
    }

    [Theory]
    [InlineData("../evil.ts")]
    [InlineData("sub/evil.ts")]
    [InlineData("a b.ts")]
    [InlineData("not-there.ts")]
    public async Task GetFile_InvalidOrMissing_ReturnsNotFound(string fileName)
    {
        var result = await _controller.GetFile(fileName);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetFile_UserWithoutTvAccess_Returns403()
    {
        _fixture.Db.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2@x.de" });
        await _fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new LiveStreamController(
            new LiveStreamSegmentStore(
                new TestWebHostEnvironment { ContentRootPath = _contentRoot },
                new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
                NullLogger<LiveStreamSegmentStore>.Instance),
            CreateStandby(),
            new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
            new FakeAuthService { CurrentUser = new ApplicationUser { Id = "u2", UserName = "u2@x.de" } },
            NullLogger<LiveStreamController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        Assert.IsType<ForbidResult>(await controller.GetFile("live.m3u8"));
    }

    [Fact]
    public async Task GetFile_AdminWithoutTvAccessRow_ReturnsFileOr404Not403()
    {
        _fixture.Db.Users.Add(new ApplicationUser { Id = "a1", UserName = "a@x.de", IsAdmin = true });
        await _fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new LiveStreamController(
            new LiveStreamSegmentStore(
                new TestWebHostEnvironment { ContentRootPath = _contentRoot },
                new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
                NullLogger<LiveStreamSegmentStore>.Instance),
            CreateStandby(),
            new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
            new FakeAuthService { CurrentUser = new ApplicationUser { Id = "a1", UserName = "a@x.de", IsAdmin = true } },
            NullLogger<LiveStreamController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Datei existiert nicht → künftig die Standby-Auslieferung; entscheidend ist: kein 403.
        Assert.IsNotType<ForbidResult>(await controller.GetFile("live.m3u8"));
    }

    [Fact]
    public async Task GetFile_NotLoggedIn_Returns401()
    {
        var controller = new LiveStreamController(
            new LiveStreamSegmentStore(
                new TestWebHostEnvironment { ContentRootPath = _contentRoot },
                new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
                NullLogger<LiveStreamSegmentStore>.Instance),
            CreateStandby(),
            new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
            new FakeAuthService(), NullLogger<LiveStreamController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.GetFile("live.m3u8");

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetFile_MissingPlaylist_ServesStandbyPlaylist()
    {
        SetQueryToken("tok 123");

        var result = await _controller.GetFile("live.m3u8");

        var content = Assert.IsType<ContentResult>(result);
        Assert.StartsWith("application/vnd.apple.mpegurl", content.ContentType);
        Assert.Contains("#EXTM3U", content.Content);
        Assert.Contains("standby_0_", content.Content);
        Assert.Contains("?access_token=tok%20123", content.Content);
        Assert.Equal("no-store", _controller.HttpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetFile_SegmentlessPlaylist_ServesStandbyPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_segDir, "live.m3u8"),
            "#EXTM3U\n#EXT-X-VERSION:3\n", ct);

        var result = await _controller.GetFile("live.m3u8");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Contains("standby_0_", content.Content);
    }

    [Fact]
    public async Task GetFile_StandbySegment_ServedFromMemory()
    {
        var result = await _controller.GetFile("standby_0_00.ts");

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("video/mp2t", file.ContentType);
        Assert.Equal(0x47, file.FileContents[0]);
        Assert.Equal("no-store", _controller.HttpContext.Response.Headers.CacheControl.ToString());

        // Auch mit vorhandener echter Playlist bleibt der aktive Standby-Satz erreichbar —
        // ein Client darf die zuletzt beworbene Playlist nicht mit 404 verlieren.
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_segDir, "live.m3u8"), "#EXTM3U\nseg_001.ts\n", ct);
        var again = await _controller.GetFile("standby_0_00.ts");
        Assert.IsType<FileContentResult>(again);
    }

    [Fact]
    public async Task GetFile_TsSegment_SetsNoStoreCacheHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_segDir, "seg_001.ts"), "segment-bytes", ct);

        var result = await _controller.GetFile("seg_001.ts");

        Assert.IsType<FileStreamResult>(result);
        Assert.Equal("no-store", _controller.HttpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetFile_RealPlaylistAfterStandby_InjectsDiscontinuity()
    {
        var ct = TestContext.Current.CancellationToken;
        await _controller.GetFile("live.m3u8"); // Standby-Auslieferung setzt das Transition-Flag
        await File.WriteAllTextAsync(Path.Combine(_segDir, "live.m3u8"),
            "#EXTM3U\n#EXT-X-VERSION:3\n#EXTINF:6.0,\nseg_001.ts\n", ct);

        var first = await _controller.GetFile("live.m3u8");
        var content = Assert.IsType<ContentResult>(first);
        var discont = content.Content!.IndexOf("#EXT-X-DISCONTINUITY", StringComparison.Ordinal);
        Assert.True(discont >= 0 && discont < content.Content.IndexOf("seg_001.ts", StringComparison.Ordinal));

        var second = await _controller.GetFile("live.m3u8");
        Assert.DoesNotContain("#EXT-X-DISCONTINUITY", Assert.IsType<ContentResult>(second).Content);
    }

    [Theory]
    [InlineData("live.m3u8")]
    [InlineData("standby_0_00.ts")]
    public async Task GetFile_FeatureDisabled_Returns404(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        (await _fixture.Db.Setups.FirstAsync(ct)).TvSenderEnabled = false;
        await _fixture.Db.SaveChangesAsync(ct);

        var result = await _controller.GetFile(fileName);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetFile_MissingPlaylist_WithoutStandbyAssets_Returns404()
    {
        var controller = new LiveStreamController(
            new LiveStreamSegmentStore(
                new TestWebHostEnvironment { ContentRootPath = _contentRoot },
                new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
                NullLogger<LiveStreamSegmentStore>.Instance),
            CreateStandby(loadDefaultSet: () => null),
            new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance),
            new FakeAuthService { CurrentUser = new ApplicationUser { Id = "u1", UserName = "u@x.de" } },
            NullLogger<LiveStreamController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        Assert.IsType<NotFoundResult>(await controller.GetFile("live.m3u8"));
    }
}
