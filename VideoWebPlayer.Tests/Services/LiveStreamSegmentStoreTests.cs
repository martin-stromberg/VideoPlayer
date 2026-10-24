using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Minimal <see cref="IWebHostEnvironment"/> test double pointing at a temp content root.
/// </summary>
internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "VideoWebPlayer.Tests";
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

/// <summary>
/// Tests for <see cref="LiveStreamSegmentStore"/>: directory resolution (default, relative and
/// absolute configured paths), strict file-name containment and the retention sweep.
/// </summary>
public sealed class LiveStreamSegmentStoreTests : IAsyncLifetime
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"vwp-segstore-{Guid.NewGuid():N}");
    private ProgramScheduleTestDb _fixture = null!;
    private ProgramSettingsService _settings = null!;
    private LiveStreamSegmentStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await ProgramScheduleTestDb.CreateAsync($"segstore-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);
        _settings = new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance);
        _store = new LiveStreamSegmentStore(
            new TestWebHostEnvironment { ContentRootPath = _contentRoot },
            _settings,
            NullLogger<LiveStreamSegmentStore>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetDirectoryAsync_Default_UsesDataLiveStreamUnderContentRoot()
    {
        var dir = await _store.GetDirectoryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_contentRoot, "Data", "LiveStream"), dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public async Task GetDirectoryAsync_ConfiguredRelative_ResolvesAgainstContentRoot()
    {
        var ct = TestContext.Current.CancellationToken;
        await _settings.UpdateLiveStreamSettingsAsync(6, "custom-segments", 30, ct);

        var dir = await _store.GetDirectoryAsync(ct);

        Assert.Equal(Path.Combine(_contentRoot, "custom-segments"), dir);
    }

    [Fact]
    public async Task GetDirectoryAsync_ConfiguredAbsolute_UsedAsIs()
    {
        var ct = TestContext.Current.CancellationToken;
        var absolute = Path.Combine(_contentRoot, "absolute-seg");
        await _settings.UpdateLiveStreamSettingsAsync(6, absolute, 30, ct);

        var dir = await _store.GetDirectoryAsync(ct);

        Assert.Equal(absolute, dir);
    }

    [Fact]
    public async Task TryResolveFileAsync_ExistingFile_ReturnsPath()
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = await _store.GetDirectoryAsync(ct);
        var file = Path.Combine(dir, "live.m3u8");
        await File.WriteAllTextAsync(file, "#EXTM3U", ct);

        Assert.Equal(file, await _store.TryResolveFileAsync("live.m3u8", ct));
    }

    [Theory]
    [InlineData("../escape.ts")]
    [InlineData("..\\escape.ts")]
    [InlineData("sub/dir.ts")]
    [InlineData("a b.ts")]
    [InlineData("")]
    [InlineData("missing.ts")]
    public async Task TryResolveFileAsync_InvalidOrMissing_ReturnsNull(string fileName)
    {
        Assert.Null(await _store.TryResolveFileAsync(fileName, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SweepExpiredSegmentsAsync_DeletesOnlyOldSegments()
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = await _store.GetDirectoryAsync(ct);
        var oldSeg = Path.Combine(dir, "seg_old_00001.ts");
        var newSeg = Path.Combine(dir, "seg_new_00001.ts");
        var playlist = Path.Combine(dir, "live.m3u8");
        await File.WriteAllTextAsync(oldSeg, "x", ct);
        await File.WriteAllTextAsync(newSeg, "x", ct);
        await File.WriteAllTextAsync(playlist, "#EXTM3U", ct);
        File.SetLastWriteTimeUtc(oldSeg, DateTime.UtcNow.AddHours(-2));

        var deleted = await _store.SweepExpiredSegmentsAsync(
            DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30), ct);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldSeg));
        Assert.True(File.Exists(newSeg));
        Assert.True(File.Exists(playlist));
    }

    [Fact]
    public async Task ClearAsync_RemovesEverything()
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = await _store.GetDirectoryAsync(ct);
        await File.WriteAllTextAsync(Path.Combine(dir, "live.m3u8"), "#EXTM3U", ct);
        await File.WriteAllTextAsync(Path.Combine(dir, "seg_a.ts"), "x", ct);

        await _store.ClearAsync(ct);

        Assert.Empty(Directory.GetFiles(dir));
    }
}
