using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="LiveStreamStandbySegmenter"/> with a fake process: argument building
/// (segment muxer, keyframe expression, 60-second duration cap), stderr draining, the
/// ffprobe duration fallback and the failure paths.
/// </summary>
public sealed class LiveStreamStandbySegmenterTests : IAsyncLifetime
{
    private sealed class FakeFfmpegLocator : IFfmpegLocator
    {
        public string? Resolve() => "/usr/bin/ffmpeg";
    }

    private sealed class FakeFfprobeLocator : IFfprobeLocator
    {
        public string? Resolve() => null; // kein ffprobe → nominaler Fallback
    }

    private sealed class FakeProcess : ISegmenterProcess
    {
        private readonly int _exitCode;
        private readonly Action? _onWait;

        public FakeProcess(int exitCode, Action? onWait = null)
        {
            _exitCode = exitCode;
            _onWait = onWait;
        }

        public bool HasExited { get; private set; }
        public int ExitCode => HasExited ? _exitCode : -1;
        public Stream StandardInput { get; } = new MemoryStream();
        public Stream StandardError { get; } = new MemoryStream();

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            _onWait?.Invoke();
            HasExited = true;
            return Task.CompletedTask;
        }

        public void Kill() => HasExited = true;
        public void Dispose() { }
    }

    /// <summary>Prozess-Attrappe, deren Warten nur per Cancellation endet (stallender Input).</summary>
    private sealed class StallingProcess : ISegmenterProcess
    {
        private readonly Action _onKill;

        public StallingProcess(Action onKill) => _onKill = onKill;

        public bool HasExited { get; private set; }
        public int ExitCode => -1;
        public Stream StandardInput { get; } = new MemoryStream();
        public Stream StandardError { get; } = new MemoryStream();

        public Task WaitForExitAsync(CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken);

        public void Kill()
        {
            _onKill();
            HasExited = true;
        }

        public void Dispose() { }
    }

    /// <summary>Prozess-Attrappe, die weder beendet werden kann noch je ein stderr-EOF
    /// liefert — der Blockierpfad, der TranscodeAsync frueher endlos haengen liess.</summary>
    private sealed class UnkillableStallingProcess : ISegmenterProcess
    {
        public bool HasExited => false;
        public int ExitCode => -1;
        public Stream StandardInput { get; } = new MemoryStream();
        public Stream StandardError { get; } = new NeverEndingStream();

        public Task WaitForExitAsync(CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken);

        public void Kill() => throw new InvalidOperationException("Kill simuliert fehlgeschlagen.");

        public void Dispose() { }
    }

    /// <summary>Stream-Attrappe, deren Leseaufruf nie abschliesst (kein stderr-EOF).</summary>
    private sealed class NeverEndingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get => 0; set { } }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => new TaskCompletionSource<int>().Task;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(new TaskCompletionSource<int>().Task);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"vwp-standby-seg-{Guid.NewGuid():N}");
    private readonly List<ProcessStartInfo> _started = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
        return ValueTask.CompletedTask;
    }

    private LiveStreamStandbySegmenter CreateSegmenter(
        Func<ProcessStartInfo, ISegmenterProcess?> startProcess, TimeSpan? transcodeTimeout = null,
        ILogger<LiveStreamStandbySegmenter>? logger = null, TimeSpan? stderrDrainTimeout = null)
        => new(new FakeFfmpegLocator(), new FakeFfprobeLocator(),
            logger ?? NullLogger<LiveStreamStandbySegmenter>.Instance,
            startProcess, transcodeTimeout, stderrDrainTimeout);

    [Fact]
    public async Task TranscodeAsync_BuildsSegmentArgs_AndAppliesDurationCap()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter(startInfo =>
        {
            _started.Add(startInfo);
            return new FakeProcess(0, onWait: () =>
                File.WriteAllBytes(Path.Combine(_cacheDir, "standby_000.ts"), [0x47, 0x01]));
        });

        var result = await segmenter.TranscodeAsync(
            LiveStreamInput.ForFile("/media/clip.mkv"), _cacheDir, ct);

        var args = _started.Single().Arguments;
        Assert.Contains("-f segment", args);
        Assert.Contains("-t 60", args);
        Assert.Contains("expr:gte(t,n_forced*4)", args);
        Assert.Contains("standby_%03d.ts", args);
        Assert.Contains("-nostdin", args);
        Assert.Contains("-i \"/media/clip.mkv\"", args);
        Assert.False(_started.Single().RedirectStandardInput);

        var segment = Assert.Single(result!);
        Assert.Equal("standby_000.ts", segment.FileName);
        Assert.Equal(TimeSpan.FromSeconds(4), segment.Duration); // ffprobe-Fallback nominal 4s
    }

    [Fact]
    public async Task TranscodeAsync_PipeInput_UsesPipeAndPumpsStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter(startInfo =>
        {
            _started.Add(startInfo);
            return new FakeProcess(0, onWait: () =>
                File.WriteAllBytes(Path.Combine(_cacheDir, "standby_000.ts"), [0x47]));
        });

        var result = await segmenter.TranscodeAsync(
            LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]))),
            _cacheDir, ct);

        Assert.Contains("-i pipe:0", _started.Single().Arguments);
        Assert.DoesNotContain("-nostdin", _started.Single().Arguments);
        Assert.True(_started.Single().RedirectStandardInput);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TranscodeAsync_ExitCodeNonZero_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter(startInfo =>
        {
            _started.Add(startInfo);
            return new FakeProcess(3);
        });

        var result = await segmenter.TranscodeAsync(
            LiveStreamInput.ForFile("/media/clip.mkv"), _cacheDir, ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task TranscodeAsync_NoSegmentsProduced_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter(_ => new FakeProcess(0));

        var result = await segmenter.TranscodeAsync(
            LiveStreamInput.ForFile("/media/clip.mkv"), _cacheDir, ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task TranscodeAsync_StallingProcess_TimesOutKillsAndReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var killed = false;
        var segmenter = CreateSegmenter(_ => new StallingProcess(() => killed = true),
            transcodeTimeout: TimeSpan.FromMilliseconds(250));

        var result = await segmenter.TranscodeAsync(
            LiveStreamInput.ForFile("/media/clip.mkv"), _cacheDir, ct);

        Assert.Null(result);
        Assert.True(killed);
    }

    [Fact]
    public async Task TranscodeAsync_KillFailsAndStderrNeverEnds_ReturnsWithinBound()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new ListLogger<LiveStreamStandbySegmenter>(new ConcurrentQueue<string>());
        var segmenter = CreateSegmenter(_ => new UnkillableStallingProcess(),
            transcodeTimeout: TimeSpan.FromMilliseconds(100), logger: logger,
            stderrDrainTimeout: TimeSpan.FromMilliseconds(250));

        // Fehlschlagendes Kill() + endloser stderr-Stream: ohne die Drain-Begrenzung hing
        // TranscodeAsync hier für immer — der äußere Rahmen hält auch den Fehlerfall
        // testbar, statt den Testprozess zu blockieren.
        var result = await segmenter.TranscodeAsync(
            LiveStreamInput.ForFile("/media/clip.mkv"), _cacheDir, ct)
            .WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);

        Assert.Null(result);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("nicht beendet"));
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("stderr-Drain"));
    }

    [Fact]
    public async Task TranscodeAsync_LockedStaleSegment_StaysOutOfResult()
    {
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(_cacheDir);
        var stalePath = Path.Combine(_cacheDir, "standby_099.ts");
        await File.WriteAllBytesAsync(stalePath, [0x47, 0x99], ct);

        var logger = new ListLogger<LiveStreamStandbySegmenter>(new ConcurrentQueue<string>());
        var segmenter = CreateSegmenter(_ => new FakeProcess(0, onWait: () =>
            File.WriteAllBytes(Path.Combine(_cacheDir, "standby_000.ts"), [0x47, 0x01])),
            logger: logger);

        // Gehaltenes Alt-Segment: unter Windows schlägt File.Delete fehl (Sharing-
        // Verletzung) und die Datei bleibt liegen, unter Unix wird sie entlinkt — in
        // keinem Fall darf der Rest unbemerkt in den neuen Satz wandern.
        await using (var lockHandle = new FileStream(stalePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            var result = await segmenter.TranscodeAsync(
                LiveStreamInput.ForFile("/media/clip.mkv"), _cacheDir, ct);

            var segment = Assert.Single(result!);
            Assert.Equal("standby_000.ts", segment.FileName);
            if (OperatingSystem.IsWindows())
            {
                Assert.Contains(logger.Entries, e =>
                    e.Level == LogLevel.Warning && e.Message.Contains("konnte nicht geloescht werden"));
            }
        }
    }

    [Fact]
    public async Task TranscodeAsync_FfmpegMissing_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = new LiveStreamStandbySegmenter(
            new MissingFfmpegLocator(), new FakeFfprobeLocator(), NullLogger<LiveStreamStandbySegmenter>.Instance);

        Assert.Null(await segmenter.TranscodeAsync(LiveStreamInput.ForFile("/x.mkv"), _cacheDir, ct));
    }

    private sealed class MissingFfmpegLocator : IFfmpegLocator
    {
        public string? Resolve() => null;
    }
}
