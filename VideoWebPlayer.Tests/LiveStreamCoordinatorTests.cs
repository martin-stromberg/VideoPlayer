using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
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
        public LiveStreamExitInfo? LastUnexpectedExit { get; set; }
        public bool SftpInputSupported { get; set; } = true;
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

    private sealed class StubStreamTokenService : IInternalMediaStreamTokenService
    {
        public string? UriToReturn { get; set; } =
            "http://127.0.0.1:5002/api/internal/media-stream/" + new string('e', 64);
        public int StreamUriCalls { get; private set; }

        public string? TryCreateProbeUri(long mediaItemId) => null;

        public string? TryCreateStreamUri(long mediaItemId, TimeSpan lifetime)
        {
            StreamUriCalls++;
            return UriToReturn;
        }

        public long? ResolveMediaItemId(string token) => null;
    }

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"vwp-coord-{Guid.NewGuid():N}");
    private ProgramScheduleTestDb _fixture = null!;
    private FakeGuide _guide = null!;
    private FakeLocator _locator = null!;
    private FakeSegmenter _segmenter = null!;
    private StubStreamTokenService _streamTokens = null!;
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
        _streamTokens = new StubStreamTokenService();
        _coordinator = CreateCoordinator();
    }

    // Produktiv wird der Koordinator pro Tick in einem frischen Scope aufgelöst —
    // Tests, die mehrere Ticks spielen, bekommen deshalb je Tick eine neue Instanz;
    // geteilt wird nur, was produktiv Singleton ist (Segmenter, Guide, Token-Dienst).
    private LiveStreamCoordinator CreateCoordinator()
    {
        var settings = new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance);
        var store = new LiveStreamSegmentStore(
            new TestWebHostEnvironment { ContentRootPath = _contentRoot },
            settings, NullLogger<LiveStreamSegmentStore>.Instance);
        var resolver = new LiveStreamMediaResolver(_fixture.Db,
            new MediaSourceReaderDispatcher(
                new SftpMediaSourceReader(),
                new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance)),
            new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance),
            _streamTokens, Options.Create(new LiveStreamOptions()),
            NullLogger<LiveStreamMediaResolver>.Instance);
        return new LiveStreamCoordinator(
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

    private async Task<MediaItem> SeedRemoteMediaItemAsync(string fileName = "remote.mkv", bool withCredentials = false)
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new MediaSource
        {
            Name = $"Sftp-{Guid.NewGuid():N}",
            Path = "/remote",
            SourceType = MediaSourceType.Sftp,
            Host = "h",
            Port = 22,
            Username = withCredentials ? "streamer" : null,
            Password = withCredentials ? "geheim" : null
        };
        _fixture.Db.MediaSources.Add(source);
        await _fixture.Db.SaveChangesAsync(ct);
        var collection = new MediaCollection { Name = "Col", MediaSourceId = source.Id, Path = "/remote", CreatedAt = DateTime.UtcNow };
        _fixture.Db.MediaCollections.Add(collection);
        await _fixture.Db.SaveChangesAsync(ct);
        var item = new MediaItem { Name = fileName, Path = $"/remote/{fileName}", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(90) };
        _fixture.Db.MediaItems.Add(item);
        await _fixture.Db.SaveChangesAsync(ct);
        return item;
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

    [Fact]
    public async Task SynchronizeAsync_RemoteMedia_UsesHttpInput()
    {
        var item = await SeedRemoteMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        var job = Assert.Single(_segmenter.Jobs);
        Assert.Equal(_streamTokens.UriToReturn, job.Input.HttpUrl);
        Assert.False(job.Input.IsPipe);
    }

    [Fact]
    public async Task SynchronizeAsync_HttpTransportFailureSameSlot_FallsBackToPipe()
    {
        var item = await SeedRemoteMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(11, 1, HttpTransportFailure: true, DateTimeOffset.UtcNow);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        var job = Assert.Single(_segmenter.Jobs);
        Assert.True(job.Input.IsPipe);
        Assert.Null(job.Input.HttpUrl);
        Assert.Equal(0, _streamTokens.StreamUriCalls);
    }

    [Fact]
    public async Task SynchronizeAsync_NonTransportExit_KeepsHttpInput()
    {
        var item = await SeedRemoteMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(11, 1, HttpTransportFailure: false, DateTimeOffset.UtcNow);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        Assert.Equal(_streamTokens.UriToReturn, Assert.Single(_segmenter.Jobs).Input.HttpUrl);
    }

    [Fact]
    public async Task SynchronizeAsync_HttpTransportFailureOtherSlot_KeepsHttpInput()
    {
        var item = await SeedRemoteMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(99, 1, HttpTransportFailure: true, DateTimeOffset.UtcNow);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        Assert.Equal(_streamTokens.UriToReturn, Assert.Single(_segmenter.Jobs).Input.HttpUrl);
    }

    [Fact]
    public async Task SynchronizeAsync_UndecodableInputSameSlot_SkipsSlotUntilEnd()
    {
        var item = await SeedLocalMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);
        _segmenter.IsRunning = true;
        _segmenter.CurrentSlotId = 11;
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(
            11, 183, HttpTransportFailure: false, DateTimeOffset.UtcNow, UndecodableInput: true);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.InputUnplayable, result.Action);
        Assert.Equal(_guide.Result.Now.EndUtc, result.WakeAtUtc);
        Assert.Empty(_segmenter.Jobs);
        Assert.True(_segmenter.StopCalls >= 1);
        Assert.Contains("nicht lesbar", result.Detail);
    }

    [Fact]
    public async Task SynchronizeAsync_UndecodableInputOtherSlot_IgnoresFailure()
    {
        var item = await SeedLocalMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(
            99, 183, HttpTransportFailure: false, DateTimeOffset.UtcNow, UndecodableInput: true);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        Assert.Single(_segmenter.Jobs);
    }

    [Fact]
    public async Task SynchronizeAsync_RemotePipeFallback_StartedDetailNamesInputKindAndNote()
    {
        _streamTokens.UriToReturn = null;
        var item = await SeedRemoteMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        Assert.True(Assert.Single(_segmenter.Jobs).Input.IsPipe);
        Assert.Contains("pipe:0", result.Detail);
        Assert.Contains("keine http-Basis-URL", result.Detail);
    }

    [Fact]
    public async Task SynchronizeAsync_RemoteMedia_StartedDetailNamesHttpInput()
    {
        var item = await SeedRemoteMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        Assert.Contains("Eingang: HTTP", result.Detail);
    }

    [Fact]
    public async Task SynchronizeAsync_HttpTransportFailureSameSlotWithCredentials_FallsBackToSftp()
    {
        var item = await SeedRemoteMediaItemAsync(withCredentials: true);
        _guide.Result.Now = Entry(11, item.Id);
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(11, 1, HttpTransportFailure: true, DateTimeOffset.UtcNow);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        var job = Assert.Single(_segmenter.Jobs);
        Assert.NotNull(job.Input.SftpUrl);
        Assert.False(job.Input.IsPipe);
        Assert.Equal(0, _streamTokens.StreamUriCalls);
        Assert.Contains("sftp", result.Detail);
    }

    [Fact]
    public async Task SynchronizeAsync_SftpFailureSameSlot_FallsBackToPipe()
    {
        var item = await SeedRemoteMediaItemAsync(withCredentials: true);
        _guide.Result.Now = Entry(11, item.Id);
        _streamTokens.UriToReturn = null; // keine http-Basis-URL → HTTP-Stufe greift nicht
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(
            11, 1, HttpTransportFailure: false, DateTimeOffset.UtcNow, SftpInputFailure: true);

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        var job = Assert.Single(_segmenter.Jobs);
        Assert.True(job.Input.IsPipe);
        Assert.Null(job.Input.SftpUrl);
    }

    [Fact]
    public async Task SynchronizeAsync_SftpUnsupported_SkipsSftpInput()
    {
        var item = await SeedRemoteMediaItemAsync(withCredentials: true);
        _guide.Result.Now = Entry(11, item.Id);
        _streamTokens.UriToReturn = null;
        _segmenter.SftpInputSupported = false;

        var result = await _coordinator.SynchronizeAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        var job = Assert.Single(_segmenter.Jobs);
        Assert.True(job.Input.IsPipe);
        Assert.Null(job.Input.SftpUrl);
    }

    [Fact]
    public async Task SynchronizeAsync_AlternatingHttpAndSftpFailures_EscalatesToPipe()
    {
        var item = await SeedRemoteMediaItemAsync(withCredentials: true);
        _guide.Result.Now = Entry(11, item.Id);
        var ct = TestContext.Current.CancellationToken;

        // Tick 1: HTTP-Transportfehler — der naechste Versuch laeuft auf der sftp-Stufe.
        // Der Exit-Report fuehrt die kumulierte Stufen-Maske des Sendeplatzes; frischer
        // Koordinator pro Tick, weil er produktiv in einem neuen Scope aufgeloest wird.
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(
            11, 1, HttpTransportFailure: true, DateTimeOffset.UtcNow, HttpInputFailedForSlot: true);
        var first = await CreateCoordinator().SynchronizeAsync(DateTimeOffset.UtcNow, ct);
        Assert.Equal(LiveStreamSyncAction.Started, first.Action);
        Assert.NotNull(_segmenter.Jobs.Last().Input.SftpUrl);

        // Der Lauf ist inzwischen gestorben — Tick 2: auch der sftp-Eingang scheitert
        // mit einem Transportfehler; der Report fuehrt beide gescheiterten Stufen.
        _segmenter.IsRunning = false;
        _segmenter.CurrentSlotId = null;
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(
            11, 1, HttpTransportFailure: false, DateTimeOffset.UtcNow,
            SftpInputFailure: true, HttpInputFailedForSlot: true, SftpInputFailedForSlot: true);
        var second = await CreateCoordinator().SynchronizeAsync(DateTimeOffset.UtcNow, ct);

        // Die Fehlschlaege kumulieren pro Sendeplatz: die Kaskade darf nicht zurueck zu
        // HTTP schwingen, sondern muss die letzte Stufe pipe:0 erreichen.
        Assert.Equal(LiveStreamSyncAction.Started, second.Action);
        var job = _segmenter.Jobs.Last();
        Assert.True(job.Input.IsPipe);
        Assert.Null(job.Input.HttpUrl);
        Assert.Null(job.Input.SftpUrl);
    }

    [Fact]
    public async Task SynchronizeAsync_PipeExitAfterHttpFailure_StaysOnPipeInput()
    {
        var item = await SeedRemoteMediaItemAsync();
        _guide.Result.Now = Entry(11, item.Id);
        var ct = TestContext.Current.CancellationToken;

        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(
            11, 1, HttpTransportFailure: true, DateTimeOffset.UtcNow, HttpInputFailedForSlot: true);
        await CreateCoordinator().SynchronizeAsync(DateTimeOffset.UtcNow, ct);
        Assert.True(_segmenter.Jobs.Last().Input.IsPipe);

        // Auch der pipe-Lauf endet unerwartet (kein Transport-Flag) — die kumulierte
        // Maske des Reports haelt die HTTP-Stufe fuer den Rest des Sendeplatzes
        // gescheitert, statt erneut zu alternieren.
        _segmenter.IsRunning = false;
        _segmenter.CurrentSlotId = null;
        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(
            11, 1, HttpTransportFailure: false, DateTimeOffset.UtcNow, HttpInputFailedForSlot: true);
        var result = await CreateCoordinator().SynchronizeAsync(DateTimeOffset.UtcNow, ct);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        Assert.True(_segmenter.Jobs.Last().Input.IsPipe);
        Assert.Equal(0, _streamTokens.StreamUriCalls);
    }

    [Fact]
    public async Task SynchronizeAsync_InputFailureMemory_ResetsOnSlotChange()
    {
        var item = await SeedRemoteMediaItemAsync(withCredentials: true);
        _guide.Result.Now = Entry(11, item.Id);
        var ct = TestContext.Current.CancellationToken;

        _segmenter.LastUnexpectedExit = new LiveStreamExitInfo(
            11, 1, HttpTransportFailure: true, DateTimeOffset.UtcNow, HttpInputFailedForSlot: true);
        await CreateCoordinator().SynchronizeAsync(DateTimeOffset.UtcNow, ct);
        Assert.NotNull(_segmenter.Jobs.Last().Input.SftpUrl);

        // Neuer Sendeplatz — die Fehlschlag-Maske gehoert zum Slot des Reports; ein
        // Report eines anderen Sendeplatzes zaehlt nicht.
        _segmenter.IsRunning = false;
        _segmenter.CurrentSlotId = null;
        _guide.Result.Now = Entry(22, item.Id);
        var result = await CreateCoordinator().SynchronizeAsync(DateTimeOffset.UtcNow, ct);

        Assert.Equal(LiveStreamSyncAction.Started, result.Action);
        Assert.Equal(_streamTokens.UriToReturn, _segmenter.Jobs.Last().Input.HttpUrl);
    }
}
