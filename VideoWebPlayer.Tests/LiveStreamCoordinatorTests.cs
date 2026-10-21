using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="LiveStreamCoordinator"/>: the time-based synchronization of the ffmpeg
/// segmentation with the broadcast schedule — starts, restarts on slot change and drift, stops
/// for gaps, and the unavailable-ffmpeg/unresolvable-media paths, against real SQLite for media
/// resolution and settings.
/// </summary>
public sealed class LiveStreamCoordinatorTests : IAsyncLifetime
{
    private sealed class FakeGuide : IProgramGuideService
    {
        public DtoProgramGuideNow Result { get; set; } = new();
        public Task<DtoProgramGuideNow> GetNowAsync(DateTimeOffset utcNow, CancellationToken ct = default)
            => Task.FromResult(Result);
        public Task<DtoProgramGuideEntry[]> GetUpcomingAsync(DateTimeOffset utcNow, TimeSpan preview, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<DtoProgramGuideEntry>());
    }

    private sealed class FakeLocator : IFfmpegLocator
    {
        public string? Path { get; set; } = "/usr/bin/ffmpeg";
        public string? Resolve() => Path;
    }

    private sealed class FakeSegmenter : ILiveStreamSegmenter
    {
        public bool IsRunning { get; set; }
        public long? CurrentSlotId { get; set; }
        public double? ExpectedPositionSeconds { get; set; }
        public List<LiveStreamJob> Jobs { get; } = [];
        public int StopCalls { get; private set; }

        public Task StartAsync(LiveStreamJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);
            IsRunning = true;
            CurrentSlotId = job.SlotId;
            ExpectedPositionSeconds = job.PositionSeconds;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            StopCalls++;
            IsRunning = false;
            CurrentSlotId = null;
            return Task.CompletedTask;
        }
    }

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"vwp-coord-{Guid.NewGuid():N}");
    private ProgramScheduleTestDb _fixture = null!;
    private FakeGuide _guide = null!;
    private FakeLocator _locator = null!;
    private FakeSegmenter _segmenter = null!;
    private LiveStreamCoordinator _coordinator = null!;
    private string _localRoot = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _fixture = await ProgramScheduleTestDb.CreateAsync($"coord-{Guid.NewGuid():N}", ct);
        _localRoot = Path.Combine(_contentRoot, "media");
        Directory.CreateDirectory(_localRoot);

        _guide = new FakeGuide();
        _locator = new FakeLocator();
        _segmenter = new FakeSegmenter();
        var settings = new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance);
        var store = new LiveStreamSegmentStore(
            new TestWebHostEnvironment { ContentRootPath = _contentRoot },
            settings, NullLogger<LiveStreamSegmentStore>.Instance);
        var resolver = new LiveStreamMediaResolver(_fixture.Db,
            new MediaSourceReaderDispatcher(
                new SftpMediaSourceReader(),
                new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance)),
            new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance),
            NullLogger<LiveStreamMediaResolver>.Instance);
        _coordinator = new LiveStreamCoordinator(
            _guide, resolver, store, _segmenter, _locator, settings,
            NullLogger<LiveStreamCoordinator>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
        await Task.CompletedTask;
    }

    private async Task<MediaItem> SeedLocalMediaItemAsync(string fileName = "movie.mkv")
    {
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_localRoot, fileName), "video", ct);
        var source = new MediaSource
        {
            Name = $"Local-{Guid.NewGuid():N}",
            Path = _localRoot,
            SourceType = MediaSourceType.LocalDirectory,
            Host = "",
            Port = 0
        };
        _fixture.Db.MediaSources.Add(source);
        await _fixture.Db.SaveChangesAsync(ct);
        var collection = new MediaCollection { Name = "Col", MediaSourceId = source.Id, Path = _localRoot, CreatedAt = DateTime.UtcNow };
        _fixture.Db.MediaCollections.Add(collection);
        await _fixture.Db.SaveChangesAsync(ct);
        var item = new MediaItem { Name = fileName, Path = Path.Combine(_localRoot, fileName), MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(90) };
        _fixture.Db.MediaItems.Add(item);
        await _fixture.Db.SaveChangesAsync(ct);
        return item;
    }

    private static DtoProgramGuideEntry Entry(long slotId, long mediaItemId, double position = 100, double remaining = 500)
        => new()
        {
            SlotId = slotId,
            MediaItemId = mediaItemId,
            Title = "Sendung",
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            EndUtc = DateTimeOffset.UtcNow.AddMinutes(8),
            DurationSeconds = 600,
            PositionSeconds = position,
            RemainingSeconds = remaining
        };

    [Fact]
    public async Task SynchronizeAsync_RunningSlot_StartsSegmentation()
    {
        var item = await SeedLocalMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id, position: 125.5, remaining: 474.5);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        var job = Assert.Single(_segmenter.Jobs);
        Assert.Equal(11, job.SlotId);
        Assert.Equal(Path.Combine(_localRoot, "movie.mkv"), job.Input.FilePath);
        Assert.Equal(125.5, job.PositionSeconds);
        Assert.Equal(474.5, job.RemainingSeconds);
        Assert.Equal(6, job.SegmentSeconds);
        Assert.Equal("/usr/bin/ffmpeg", job.FfmpegPath);
        Assert.Equal(_guide.Result.Now.EndUtc, result.WakeAtUtc);
    }

    [Fact]
    public async Task SynchronizeAsync_SameSlotWithinDrift_Unchanged()
    {
        var item = await SeedLocalMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id, position: 130);
        _segmenter.IsRunning = true;
        _segmenter.CurrentSlotId = 11;
        _segmenter.ExpectedPositionSeconds = 128;

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Unchanged, result.Action);
        Assert.Empty(_segmenter.Jobs);
    }

    [Fact]
    public async Task SynchronizeAsync_SameSlotBeyondDrift_Restarts()
    {
        var item = await SeedLocalMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id, position: 200);
        _segmenter.IsRunning = true;
        _segmenter.CurrentSlotId = 11;
        _segmenter.ExpectedPositionSeconds = 100;

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        var job = Assert.Single(_segmenter.Jobs);
        Assert.Equal(200, job.PositionSeconds);
    }

    [Fact]
    public async Task SynchronizeAsync_DifferentSlot_RestartsOnNewSlot()
    {
        var item = await SeedLocalMediaItemAsync();
        _guide.Result.Now = Entry(22, item.Id);
        _segmenter.IsRunning = true;
        _segmenter.CurrentSlotId = 11;
        _segmenter.ExpectedPositionSeconds = 100;

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        Assert.Equal(22, Assert.Single(_segmenter.Jobs).SlotId);
    }

    [Fact]
    public async Task SynchronizeAsync_ScheduleGap_StopsAndWakesAtNextStart()
    {
        _guide.Result.Now = null;
        _guide.Result.Next = Entry(33, 99);
        _segmenter.IsRunning = true;
        _segmenter.CurrentSlotId = 11;

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Stopped, result.Action);
        Assert.Equal(_guide.Result.Next.StartUtc, result.WakeAtUtc);
        Assert.Equal(1, _segmenter.StopCalls);
    }

    [Fact]
    public async Task SynchronizeAsync_FfmpegMissing_ReportsUnavailable()
    {
        _locator.Path = null;
        _guide.Result.Now = Entry(11, 1);
        _segmenter.IsRunning = true;

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.FfmpegUnavailable, result.Action);
        Assert.Empty(_segmenter.Jobs);
        Assert.Equal(1, _segmenter.StopCalls);
    }

    [Fact]
    public async Task SynchronizeAsync_UnresolvableMedia_ReportsMediaUnavailable()
    {
        _guide.Result.Now = Entry(11, mediaItemId: 98765);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.MediaUnavailable, result.Action);
        Assert.Empty(_segmenter.Jobs);
    }
}
