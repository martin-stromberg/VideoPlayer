using Microsoft.Extensions.Logging;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the first-output watchdog of <see cref="FfmpegLiveStreamSegmenter"/> (Issue
/// #319): the wall time from process start to the first new <c>seg_*_{slotId}_*.ts</c> or
/// the first playlist rewrite is logged — on non-seekable inputs (<c>pipe:0</c>, index-less
/// containers) a <c>-re</c>-throttled forward scan delays that first segment toward
/// <c>PositionSeconds</c>, which was previously invisible to logs and the drift watcher.
/// </summary>
public sealed class FfmpegLiveStreamSegmenterTests_FirstOutput : IDisposable
{
    /// <summary>stderr-Ersatz, der Reads blockiert bis <see cref="End"/> — ein
    /// sofortiges EOF würde den Exit-Report auslösen und die Log-Assertions verwässern.</summary>
    private sealed class BlockingStream : Stream
    {
        private readonly SemaphoreSlim _signal = new(0);
        private bool _ended;

        public void End()
        {
            _ended = true;
            _signal.Release();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (_ended)
                    return 0;
                await _signal.WaitAsync(cancellationToken);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Minimale <see cref="ISegmenterProcess"/>-Attrappe mit blockierendem stderr.</summary>
    private sealed class FakeSegmenterProcess : ISegmenterProcess
    {
        public bool HasExited { get; private set; }
        public int ExitCode { get; private set; } = -1;
        public Stream StandardInput { get; } = new MemoryStream();
        public BlockingStream Stderr { get; } = new();
        public Stream StandardError => Stderr;

        /// <summary>Simuliert das eigenständige Prozessende: Exit-Status plus stderr-EOF.</summary>
        public void SimulateExit(int exitCode = 1)
        {
            ExitCode = exitCode;
            HasExited = true;
            Stderr.End();
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            HasExited = true;
            return Task.CompletedTask;
        }

        public void Kill() { HasExited = true; Stderr.End(); }
        public void Dispose() { }
    }

    private readonly List<string> _tempDirs = [];
    private readonly List<FakeSegmenterProcess> _processes = [];
    private readonly ListLogger<FfmpegLiveStreamSegmenter> _logger = new(new());

    private FfmpegLiveStreamSegmenter CreateSegmenter(TimeSpan? firstOutputWarnAfter = null)
        => new(_logger, _ =>
        {
            var process = new FakeSegmenterProcess();
            _processes.Add(process);
            return process;
        }, TimeProvider.System, pumpDrainTimeout: null, firstOutputWarnAfter);

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vwp-firstout-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static LiveStreamJob Job(string segmentDirectory, long slotId = 42)
        => new(slotId, LiveStreamInput.ForFile("/media/movie.mkv"), 125.5, 3600, 6,
            segmentDirectory, "/usr/bin/ffmpeg");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10, ct);
        Assert.True(condition(), "Bedingung wurde innerhalb des Timeouts nicht erfüllt.");
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception) { }
        }
    }

    [Fact]
    public async Task StartAsync_NewSegmentNameAfterStart_LogsFirstOutputLatency()
    {
        var dir = NewTempDir();
        // Rest einer früheren Generation: der Name existiert schon beim Start-Snapshot
        // und darf nicht als erste Ausgabe des neuen Laufs zählen — bei append_list
        // setzt der %05d-Zähler ohnehin nicht bei 00000 an.
        File.WriteAllText(Path.Combine(dir, "seg_20200101000000_42_00003.ts"), "alt");
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(Job(dir), TestContext.Current.CancellationToken);

        await Task.Delay(700, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("Erstes Segment"));

        File.WriteAllText(Path.Combine(dir, "seg_20261101010101_42_00004.ts"), "neu");

        await WaitUntilAsync(() => _logger.Entries.Any(
            e => e.Level == LogLevel.Information && e.Message.Contains("Erstes Segment")));
        var entry = _logger.Entries.Single(
            e => e.Level == LogLevel.Information && e.Message.Contains("Erstes Segment"));
        Assert.Contains("Sendeplatz 42", entry.Message);
        Assert.Contains("Eingang: Datei", entry.Message);
        Assert.Contains("125", entry.Message); // Position (Dezimaltrenner kulturabhängig)
    }

    [Fact]
    public async Task StartAsync_PlaylistRewrite_LogsFirstOutput()
    {
        var dir = NewTempDir();
        var playlist = Path.Combine(dir, "live.m3u8");
        File.WriteAllText(playlist, "#EXTM3U\n");
        File.SetLastWriteTimeUtc(playlist, DateTime.UtcNow.AddMinutes(-10));
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(Job(dir), TestContext.Current.CancellationToken);

        await Task.Delay(700, TestContext.Current.CancellationToken);
        File.WriteAllText(playlist, "#EXTM3U\n#EXTINF:6.0,\nseg_x_42_00000.ts\n");

        await WaitUntilAsync(() => _logger.Entries.Any(
            e => e.Level == LogLevel.Information && e.Message.Contains("Erstes Segment")));
    }

    [Fact]
    public async Task StartAsync_SegmentOfOtherSlot_NotCountedAsFirstOutput()
    {
        var dir = NewTempDir();
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(Job(dir, slotId: 42), TestContext.Current.CancellationToken);

        File.WriteAllText(Path.Combine(dir, "seg_20261101010101_99_00000.ts"), "fremd");
        await Task.Delay(1200, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("Erstes Segment"));
    }

    [Fact]
    public async Task StartAsync_NoOutputWithinThreshold_LogsLatencyWarning()
    {
        var dir = NewTempDir();
        var segmenter = CreateSegmenter(firstOutputWarnAfter: TimeSpan.FromSeconds(1));
        await segmenter.StartAsync(Job(dir), TestContext.Current.CancellationToken);

        await WaitUntilAsync(() => _logger.Entries.Any(
            e => e.Level == LogLevel.Warning && e.Message.Contains("Kein erstes Segment")));

        var warning = _logger.Entries.Single(
            e => e.Level == LogLevel.Warning && e.Message.Contains("Kein erstes Segment"));
        Assert.Contains("Sendeplatz 42", warning.Message);
        Assert.Contains("Scan-Latenz", warning.Message);
        Assert.Contains("Eingang Datei", warning.Message);
    }

    [Fact]
    public async Task StopAsync_BeforeThreshold_NoLatencyWarning()
    {
        var dir = NewTempDir();
        var segmenter = CreateSegmenter(firstOutputWarnAfter: TimeSpan.FromSeconds(1));
        await segmenter.StartAsync(Job(dir), TestContext.Current.CancellationToken);

        await segmenter.StopAsync(TestContext.Current.CancellationToken);
        await Task.Delay(1600, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(_logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Kein erstes Segment"));
    }

    [Fact]
    public async Task UnexpectedExit_BeforeFirstOutput_ReportsSegmentAusgeblieben()
    {
        var dir = NewTempDir();
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(Job(dir), TestContext.Current.CancellationToken);

        _processes.Single().SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(
            e => e.Level == LogLevel.Warning && e.Message.Contains("unerwartet")));
        var warning = _logger.Entries.Single(
            e => e.Level == LogLevel.Warning && e.Message.Contains("unerwartet"));
        Assert.Contains("erstes Segment: ausgeblieben", warning.Message);
    }

    [Fact]
    public async Task UnexpectedExit_AfterFirstOutput_ReportsSegmentErzeugt()
    {
        var dir = NewTempDir();
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(Job(dir), TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(dir, "seg_20261101010101_42_00000.ts"), "neu");
        await WaitUntilAsync(() => _logger.Entries.Any(
            e => e.Level == LogLevel.Information && e.Message.Contains("Erstes Segment")));

        _processes.Single().SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(
            e => e.Level == LogLevel.Warning && e.Message.Contains("unerwartet")));
        var warning = _logger.Entries.Single(
            e => e.Level == LogLevel.Warning && e.Message.Contains("unerwartet"));
        Assert.Contains("erstes Segment: erzeugt", warning.Message);
    }
}
