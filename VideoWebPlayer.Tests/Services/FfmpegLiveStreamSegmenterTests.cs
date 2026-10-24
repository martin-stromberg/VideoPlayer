using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="FfmpegLiveStreamSegmenter"/> with a fake process: argument building,
/// file vs. HTTP vs. pipe input, lifecycle (start/stop/running state), restart semantics and
/// the unexpected-exit diagnosis (stderr tail, exit code, HTTP transport classification).
/// </summary>
public sealed class FfmpegLiveStreamSegmenterTests
{
    /// <summary>
    /// A writeable/readable stream that blocks reads until data is written or the stream is
    /// closed — models a real process stderr, which only reaches EOF when the process dies.
    /// </summary>
    private sealed class ControlledPipeStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly SemaphoreSlim _signal = new(0);
        private long _readPos;
        private bool _closed;

        public void WriteText(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            _inner.Write(bytes, 0, bytes.Length);
            _signal.Release();
        }

        public void End()
        {
            _closed = true;
            _signal.Release(4);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var available = _inner.Length - _readPos;
                if (available > 0)
                {
                    var count = (int)Math.Min(available, buffer.Length);
                    _inner.Position = _readPos;
                    var read = await _inner.ReadAsync(buffer[..count], cancellationToken);
                    _readPos += read;
                    return read;
                }

                if (_closed)
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
        public override long Position { get => _readPos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FakeSegmenterProcess : ISegmenterProcess
    {
        private int? _exitCode;

        public bool HasExited { get; set; }
        public int ExitCode { get => _exitCode ?? (HasExited ? 0 : -1); set => _exitCode = value; }
        public Stream StandardInput { get; } = new MemoryStream();
        public ControlledPipeStream StderrPipe { get; } = new();
        public Stream StandardError => StderrPipe;
        public bool Killed { get; private set; }

        /// <summary>Simulates the process dying on its own: exit state plus stderr EOF.</summary>
        public void SimulateExit(int exitCode = 1)
        {
            _exitCode = exitCode;
            HasExited = true;
            StderrPipe.End();
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            HasExited = true;
            return Task.CompletedTask;
        }

        public void Kill() { Killed = true; HasExited = true; StderrPipe.End(); }
        public void Dispose() { }
    }

    private readonly List<ProcessStartInfo> _started = [];
    private readonly List<FakeSegmenterProcess> _processes = [];
    private readonly ListLogger<FfmpegLiveStreamSegmenter> _logger = new(new());

    private FfmpegLiveStreamSegmenter CreateSegmenter()
        => new(_logger, startInfo =>
        {
            _started.Add(startInfo);
            var process = new FakeSegmenterProcess();
            _processes.Add(process);
            return process;
        }, TimeProvider.System);

    private static LiveStreamJob FileJob(long slotId = 42)
        => new(slotId, LiveStreamInput.ForFile("/media/movie.mkv"), 125.5, 240.25, 6,
            "/segments", "/usr/bin/ffmpeg");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10, ct);
        Assert.True(condition(), "Bedingung wurde innerhalb des Timeouts nicht erfüllt.");
    }

    [Fact]
    public async Task StartAsync_FileInput_BuildsExpectedArguments()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob(), TestContext.Current.CancellationToken);

        var args = _started.Single().Arguments;
        Assert.Contains("-re", args);
        Assert.Contains("-ss 125.500", args);
        Assert.Contains("-nostdin", args);
        Assert.Contains("-i \"/media/movie.mkv\"", args);
        Assert.Contains("-c:v libx264", args);
        Assert.Contains("-c:a aac", args);
        Assert.Contains("-f hls", args);
        Assert.Contains("-hls_time 6", args);
        Assert.Contains("-hls_list_size 12", args);
        Assert.Contains("delete_segments", args);
        Assert.Contains("append_list", args);
        Assert.Contains("discont_start", args);
        Assert.Contains("program_date_time", args);
        Assert.Matches(@"seg_\d{14}_42_%05d\.ts", args);
        Assert.Contains("-t 240.250", args);
        Assert.EndsWith("live.m3u8\"", args);
        Assert.False(_started.Single().RedirectStandardInput);
    }

    [Fact]
    public async Task StartAsync_HttpInput_UsesUrlArgumentAndNoStdin()
    {
        var segmenter = CreateSegmenter();
        var url = "http://127.0.0.1:5002/api/internal/media-stream/" + new string('a', 64);
        await segmenter.StartAsync(FileJob() with { Input = LiveStreamInput.ForHttp(url) },
            TestContext.Current.CancellationToken);

        var args = _started.Single().Arguments;
        Assert.Contains($"-i \"{url}\"", args);
        Assert.Contains("-nostdin", args);
        Assert.False(_started.Single().RedirectStandardInput);
    }

    [Fact]
    public async Task StartAsync_PipeInput_UsesPipeArgumentAndRedirectsStdin()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream()))
        }, TestContext.Current.CancellationToken);

        var args = _started.Single().Arguments;
        Assert.Contains("-i pipe:0", args);
        Assert.DoesNotContain("-nostdin", args);
        Assert.True(_started.Single().RedirectStandardInput);
    }

    [Fact]
    public async Task StartAsync_SetsRunningStateAndSlot()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob(slotId: 7), TestContext.Current.CancellationToken);

        Assert.True(segmenter.IsRunning);
        Assert.Equal(7, segmenter.CurrentSlotId);
        Assert.NotNull(segmenter.ExpectedPositionSeconds);
    }

    [Fact]
    public async Task StopAsync_KillsProcessAndClearsState()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob(), TestContext.Current.CancellationToken);
        await segmenter.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(segmenter.IsRunning);
        Assert.Null(segmenter.CurrentSlotId);
        Assert.True(_processes.Single().Killed);
    }

    [Fact]
    public async Task StopAsync_Kill_IsNotReportedAsUnexpectedExit()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with { RemainingSeconds = 3600 },
            TestContext.Current.CancellationToken);

        await segmenter.StopAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Debug && e.Message.Contains("wurde angehalten")));

        Assert.Null(segmenter.LastUnexpectedExit);
        Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task UnexpectedExit_LogsWarningWithExitCodeAndStderrTail()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with { RemainingSeconds = 3600 },
            TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText("Error while decoding stream\nConversion failed!\n");
        process.SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));

        var exit = segmenter.LastUnexpectedExit!;
        Assert.Equal(42, exit.SlotId);
        Assert.Equal(1, exit.ExitCode);
        Assert.False(exit.HttpTransportFailure);
        var warning = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("Exit-Code 1", warning.Message);
        Assert.Contains("Conversion failed!", warning.Message);
    }

    [Fact]
    public async Task UnexpectedExit_MasksStreamTokenInStderr()
    {
        var segmenter = CreateSegmenter();
        var token = new string('b', 64);
        var url = $"http://127.0.0.1:5002/api/internal/media-stream/{token}";
        await segmenter.StartAsync(FileJob() with { Input = LiveStreamInput.ForHttp(url), RemainingSeconds = 3600 },
            TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText($"{url}: Server returned 404 Not Found\n");
        process.SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));

        Assert.NotNull(segmenter.LastUnexpectedExit);
        var warning = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("/api/internal/media-stream/***", warning.Message);
        Assert.DoesNotContain(token, warning.Message);
        // Auch das Debug-Log der ffmpeg-Argumente darf das Token nicht enthalten.
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains(token));
    }

    [Fact]
    public async Task UnexpectedExit_HttpInputTransportMarker_SetsHttpTransportFailure()
    {
        var segmenter = CreateSegmenter();
        var url = "http://127.0.0.1:5002/api/internal/media-stream/" + new string('c', 64);
        await segmenter.StartAsync(FileJob() with { Input = LiveStreamInput.ForHttp(url), RemainingSeconds = 3600 },
            TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText("Server returned 404 Not Found\n");
        process.SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        Assert.True(segmenter.LastUnexpectedExit!.HttpTransportFailure);
    }

    [Fact]
    public async Task UnexpectedExit_PipeInputTransportMarker_NoHttpTransportFlag()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream())),
            RemainingSeconds = 3600
        }, TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText("Server returned 404 Not Found\n");
        process.SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        Assert.False(segmenter.LastUnexpectedExit!.HttpTransportFailure);
    }

    [Fact]
    public async Task ExpectedEndOfRun_NoWarningAndNoUnexpectedExit()
    {
        var segmenter = CreateSegmenter();
        // Restlaufzeit innerhalb der Toleranz: ein Exit am Laufende ist planmäßig.
        await segmenter.StartAsync(FileJob() with { RemainingSeconds = 5 },
            TestContext.Current.CancellationToken);

        _processes.Single().SimulateExit(0);
        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Message.Contains("endete planmaessig")));

        Assert.Null(segmenter.LastUnexpectedExit);
        Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task StartAsync_ClearsLastUnexpectedExit()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with { RemainingSeconds = 3600 },
            TestContext.Current.CancellationToken);
        _processes.Single().SimulateExit(1);
        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        Assert.NotNull(segmenter.LastUnexpectedExit);

        await segmenter.StartAsync(FileJob(slotId: 43), TestContext.Current.CancellationToken);

        Assert.Null(segmenter.LastUnexpectedExit);
    }

    [Fact]
    public async Task StartAsync_SecondJob_StopsPreviousProcess()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob(slotId: 1), TestContext.Current.CancellationToken);
        await segmenter.StartAsync(FileJob(slotId: 2), TestContext.Current.CancellationToken);

        Assert.Equal(2, _processes.Count);
        Assert.True(_processes[0].Killed);
        Assert.False(_processes[1].Killed);
        Assert.Equal(2, segmenter.CurrentSlotId);
    }

    [Fact]
    public async Task StartAsync_FailedStart_ThrowsAndStaysStopped()
    {
        var segmenter = new FfmpegLiveStreamSegmenter(
            NullLogger<FfmpegLiveStreamSegmenter>.Instance, _ => null, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() => segmenter.StartAsync(FileJob(), TestContext.Current.CancellationToken));
        Assert.False(segmenter.IsRunning);
    }

    [Fact]
    public void ExpectedPositionSeconds_NoJob_IsNull()
    {
        var segmenter = CreateSegmenter();
        Assert.Null(segmenter.ExpectedPositionSeconds);
    }
}
