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

    /// <summary>
    /// stdin-Ersatz, der nach <see cref="FakeSegmenterProcess.Kill"/> beim Schreiben eine
    /// <see cref="IOException"/> wirft — modelliert den Pipe-Bruch beim geplanten Stop.
    /// </summary>
    private sealed class BrokenPipeInputStream : Stream
    {
        private readonly FakeSegmenterProcess _process;
        private readonly MemoryStream _inner = new();

        public BrokenPipeInputStream(FakeSegmenterProcess process) => _process = process;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_process.Killed)
                throw new IOException("Broken pipe (simulierter Pipe-Bruch nach Kill).");
            _inner.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_process.Killed)
                throw new IOException("Broken pipe (simulierter Pipe-Bruch nach Kill).");
            return _inner.WriteAsync(buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>Quell-Stream, der unbegrenzt Daten liefert — die Eingabe-Pumpe schreibt
    /// dadurch weiter, bis die Pipe beim Prozessende bricht.</summary>
    private sealed class EndlessReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get => 0; set { } }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(count, 1024);
            Array.Fill(buffer, (byte)0xAA, offset, n);
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer, offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = Math.Min(buffer.Length, 1024);
            buffer.Span[..n].Fill(0xAA);
            return new(n);
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Quell-Stream, dessen Leseaufruf nie abschliesst — modelliert eine stallende
    /// Quelle, in der die Eingabe-Pumpe trotz Pipe-Bruch nicht aufwacht.</summary>
    private sealed class NeverEndingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get => 0; set { } }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => new TaskCompletionSource<int>().Task;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(new TaskCompletionSource<int>().Task);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Quell-Stream, dessen Leseaufruf erst nach <see cref="Release"/> Daten
    /// liefert — modelliert eine Pumpe, die das stderr-Ende des Prozesses ueberlebt und
    /// dann doch noch ein (unvollstaendiges) Ergebnis abliefert.</summary>
    private sealed class GatedReadStream : Stream
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly byte[] _payload;
        private int _position;

        public GatedReadStream(byte[] payload, long declaredLength)
        {
            _payload = payload;
            Length = declaredLength;
        }

        public void Release() => _gate.TrySetResult();

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position { get => _position; set => _position = (int)value; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _gate.Task;
            var remaining = _payload.Length - _position;
            if (remaining <= 0)
                return 0;
            var n = Math.Min(buffer.Length, remaining);
            _payload.AsMemory(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FakeSegmenterProcess : ISegmenterProcess
    {
        private int? _exitCode;

        public bool HasExited { get; set; }
        public int ExitCode { get => _exitCode ?? (HasExited ? 0 : -1); set => _exitCode = value; }
        public Stream StandardInput { get; private set; } = new MemoryStream();
        public ControlledPipeStream StderrPipe { get; } = new();
        public Stream StandardError => StderrPipe;
        public bool Killed { get; private set; }

        /// <summary>Ersetzt stdin durch einen Stream, der nach <see cref="Kill"/> beim
        /// Schreiben einen Pipe-Bruch wirft.</summary>
        public void UseBrokenPipeStandardInput() => StandardInput = new BrokenPipeInputStream(this);

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

    private FfmpegLiveStreamSegmenter CreateSegmenter(
        Action<FakeSegmenterProcess>? configure = null,
        TimeSpan? pumpDrainTimeout = null)
        => new(_logger, startInfo =>
        {
            _started.Add(startInfo);
            var process = new FakeSegmenterProcess();
            configure?.Invoke(process);
            _processes.Add(process);
            return process;
        }, TimeProvider.System, pumpDrainTimeout);

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

    [Fact]
    public async Task StopAsync_PipeInputPumpBreak_LogsCompactDebugBeforeStopReturns()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter(p => p.UseBrokenPipeStandardInput());
        await segmenter.StartAsync(FileJob(slotId: 5) with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new EndlessReadStream()))
        }, ct);

        await segmenter.StopAsync(ct);

        // Durch das Einholen des Pump-Tasks ist die kompakte Meldung bei Rückkehr von
        // StopAsync bereits geschrieben — deterministisch, ohne Polling.
        var entries = _logger.Entries.ToArray();
        var pumpIndex = Array.FindIndex(entries, e =>
            e.Level == LogLevel.Debug && e.Message.Contains("Slot 5")
            && e.Message.Contains("Eingabe-Pump beendet"));
        Assert.True(pumpIndex >= 0, "Der kompakte Pump-Ende-Debug-Eintrag fehlt nach StopAsync.");
        var pumpEntry = entries[pumpIndex];
        Assert.Contains("IOException", pumpEntry.Message);
        Assert.DoesNotContain("System.IO.IOException", pumpEntry.Message); // kein Ausnahme-ToString()
        Assert.Null(pumpEntry.Exception); // kein Ausnahmeobjekt → kein angehängter Stacktrace
        var stopIndex = Array.FindIndex(entries, e =>
            e.Level == LogLevel.Information && e.Message.Contains("Sendeplatz 5 beendet"));
        Assert.True(stopIndex > pumpIndex,
            "Der Pump-Ende-Eintrag muss vor der 'beendet'-Meldung liegen.");
    }

    [Fact]
    public async Task StopAsync_StalledPump_ReturnsWithinPumpDrainTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter(pumpDrainTimeout: TimeSpan.FromMilliseconds(200));
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new NeverEndingReadStream()))
        }, ct);

        // Die Pumpe bleibt trotz Kill in source.ReadAsync haengen — StopAsync darf nur
        // bis zur injizierten Schranke warten (der äußere Rahmen sichert den Fehlerfall ab).
        var elapsed = Stopwatch.StartNew();
        await segmenter.StopAsync(ct).WaitAsync(TimeSpan.FromSeconds(20), CancellationToken.None);
        elapsed.Stop();

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5),
            $"StopAsync dauerte {elapsed.Elapsed} — die Pump-Drain-Schranke greift nicht.");
        Assert.False(segmenter.IsRunning);

        // Der verwaiste Pump wird nicht still fallengelassen: der Timeout meldet sich
        // als Warnung mit Slot-Kontext (wie der stderr-Drain im Standby-Segmenter).
        var warning = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("Eingabe-Pumpe", warning.Message);
        Assert.Contains("Sendeplatz 42", warning.Message);
        Assert.Contains("nicht eingeholt", warning.Message);
    }

    [Fact]
    public async Task UnexpectedExit_PipeDemuxMarkers_SetsUndecodableInput()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream())),
            RemainingSeconds = 3600
        }, TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText(
            "[mov,mp4,m4a,3gp,3g2,mj2 @ 0x55] stream 0, offset 0x30: partial file\n"
            + "Could not find codec parameters for stream 0: unspecified pixel format\n"
            + "Cannot determine format of input 0:0 after EOF\n");
        process.SimulateExit(183);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        Assert.True(segmenter.LastUnexpectedExit!.UndecodableInput);
        Assert.False(segmenter.LastUnexpectedExit.HttpTransportFailure);
    }

    [Fact]
    public async Task UnexpectedExit_FileInputDemuxMarkers_SetsUndecodableInput()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with { RemainingSeconds = 3600 },
            TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText("/media/movie.mkv: Invalid data found when processing input\n");
        process.SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        Assert.True(segmenter.LastUnexpectedExit!.UndecodableInput);
    }

    [Fact]
    public async Task UnexpectedExit_PipeInput_IncludesPumpStatsInWarning()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3, 4]))),
            RemainingSeconds = 3600
        }, TestContext.Current.CancellationToken);

        // Erst den Pump-Abschluss abwarten — erst dann trägt die Exit-Diagnose die
        // Bytezählung in die Warnzeile ein.
        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Message.Contains("vollständig")));

        var process = _processes.Single();
        process.StderrPipe.WriteText("Cannot determine format of input 0:0 after EOF\n");
        process.SimulateExit(183);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        var warning = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("Eingabe-Pumpe: 4/4 Bytes", warning.Message);
    }

    [Fact]
    public async Task UnexpectedExit_PipePumpSourceUnavailable_DemuxMarkersNoUndecodableInput()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(null)),
            RemainingSeconds = 3600
        }, TestContext.Current.CancellationToken);

        // Erst den Pump-Abschluss abwarten — erst dann fließt sein Ergebnis in die
        // Exit-Klassifikation ein.
        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Message.Contains("nicht verfuegbar")));

        var process = _processes.Single();
        process.StderrPipe.WriteText("Cannot determine format of input 0:0 after EOF\n");
        process.SimulateExit(183);

        // Die Demux-Marker sind die erwartete Folge der fehlenden Eingabe — eine
        // transiente Quell-Störung darf nicht als nicht-demuxbares Medium klassifiziert
        // werden, sonst überspringt der Coordinator den Sendeplatz statt zu wiederholen.
        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is not null);
        Assert.False(segmenter.LastUnexpectedExit!.UndecodableInput);
    }

    [Fact]
    public async Task UnexpectedExit_PipePumpTruncated_DemuxMarkersNoUndecodableInput()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(
                new TruncatingReadStream([1, 2, 3], declaredLength: 1000))),
            RemainingSeconds = 3600
        }, TestContext.Current.CancellationToken);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Message.Contains("unvollständig")));

        var process = _processes.Single();
        process.StderrPipe.WriteText("stream 0, offset 0x30: partial file\n");
        process.SimulateExit(183);

        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is not null);
        Assert.False(segmenter.LastUnexpectedExit!.UndecodableInput);
    }

    [Fact]
    public async Task UnexpectedExit_PipePumpFinishesAfterStderrEof_DemuxMarkersNoUndecodableInput()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new GatedReadStream([1, 2, 3], declaredLength: 1000);
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(source)),
            RemainingSeconds = 3600
        }, ct);

        // Der Prozess endet, waehrend die Pumpe noch auf die Quelle wartet — die
        // Klassifikation muss das Pump-Ergebnis einholen, statt mit „Pump unbekannt"
        // den Sendeplatz als nicht-demuxbar zu ueberspringen. Das spaete Ergebnis ist
        // Truncated (3 von 1000 Bytes) — die Demux-Marker sind seine Folge, nicht
        // die Ursache.
        var process = _processes.Single();
        process.StderrPipe.WriteText("stream 0, offset 0x30: partial file\n");
        process.SimulateExit(183);
        await Task.Delay(300, ct);
        source.Release();

        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is not null);
        Assert.False(segmenter.LastUnexpectedExit!.UndecodableInput);
    }

    [Fact]
    public async Task UnexpectedExit_PipePumpOutcomeUnknownPastBound_DemuxMarkersNoUndecodableInput()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter(pumpDrainTimeout: TimeSpan.FromMilliseconds(200));
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new NeverEndingReadStream())),
            RemainingSeconds = 3600
        }, ct);

        // Die Pumpe bleibt in source.ReadAsync haengen und wird nach der Schranke
        // verwaist — ihr unbekannter Ausgang darf die Demux-Marker nicht als
        // „Eingang nicht lesbar" werten lassen (Retry statt Slot-Skip).
        var process = _processes.Single();
        process.StderrPipe.WriteText("Cannot determine format of input 0:0 after EOF\n");
        process.SimulateExit(183);

        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is not null);
        Assert.False(segmenter.LastUnexpectedExit!.UndecodableInput);
    }

    [Fact]
    public async Task StartAsync_SftpInput_UsesUrlArgumentAndNoStdin()
    {
        var segmenter = CreateSegmenter();
        var url = "sftp://user:pass@host:22/remote/movie.mkv";
        await segmenter.StartAsync(FileJob() with { Input = LiveStreamInput.ForSftp(url) },
            TestContext.Current.CancellationToken);

        var args = _started.Single().Arguments;
        Assert.Contains($"-i \"{url}\"", args);
        Assert.Contains("-nostdin", args);
        Assert.False(_started.Single().RedirectStandardInput);
        Assert.True(segmenter.SftpInputSupported);
    }

    [Fact]
    public async Task UnexpectedExit_SftpInputTransportMarkers_SetsSftpInputFailure()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForSftp("sftp://user:pass@host:22/remote/movie.mkv"),
            RemainingSeconds = 3600
        }, TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText("sftp://user:pass@host:22/remote/movie.mkv: Connection refused\n");
        process.SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        Assert.True(segmenter.LastUnexpectedExit!.SftpInputFailure);
        Assert.False(segmenter.LastUnexpectedExit.HttpTransportFailure);
        Assert.False(segmenter.LastUnexpectedExit.UndecodableInput);
        Assert.True(segmenter.SftpInputSupported); // Transport-Fehler latches nicht
    }

    [Fact]
    public async Task UnexpectedExit_SftpSchemeMarkers_LatchesSftpInputSupportedFalse()
    {
        var segmenter = CreateSegmenter();
        await segmenter.StartAsync(FileJob() with
        {
            Input = LiveStreamInput.ForSftp("sftp://user:pass@host:22/remote/movie.mkv"),
            RemainingSeconds = 3600
        }, TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText("sftp://user:pass@host:22/remote/movie.mkv: Protocol not found\n");
        process.SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        Assert.True(segmenter.LastUnexpectedExit!.SftpInputFailure);
        Assert.False(segmenter.SftpInputSupported);
    }

    [Fact]
    public async Task UnexpectedExit_SftpInput_MasksCredentialsInStderr()
    {
        var segmenter = CreateSegmenter();
        var url = "sftp://streamer:geheimwort@host:22/remote/movie.mkv";
        await segmenter.StartAsync(FileJob() with { Input = LiveStreamInput.ForSftp(url), RemainingSeconds = 3600 },
            TestContext.Current.CancellationToken);

        var process = _processes.Single();
        process.StderrPipe.WriteText($"{url}: Connection refused\n");
        process.SimulateExit(1);

        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level == LogLevel.Warning));
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("geheimwort"));
        Assert.Contains("sftp://***@", _logger.Entries.Single(e => e.Level == LogLevel.Warning).Message);
    }

    [Fact]
    public async Task StartAsync_SlotSwitch_CollectsPreviousPump()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter(p => p.UseBrokenPipeStandardInput());
        var open = (CancellationToken _) => Task.FromResult<Stream?>(new EndlessReadStream());
        await segmenter.StartAsync(FileJob(slotId: 1) with { Input = LiveStreamInput.ForStream(open) }, ct);

        // Sendeplatz-Wechsel: StartAsync stoppt den alten Job intern und holt dessen
        // Pumpe ein — deren Ende-Eintrag liegt vor der Start-Meldung des neuen Slots.
        await segmenter.StartAsync(FileJob(slotId: 2) with { Input = LiveStreamInput.ForStream(open) }, ct);

        var entries = _logger.Entries.ToArray();
        var pumpIndex = Array.FindIndex(entries, e =>
            e.Level == LogLevel.Debug && e.Message.Contains("Slot 1")
            && e.Message.Contains("Eingabe-Pump beendet"));
        Assert.True(pumpIndex >= 0,
            "Die Pumpe des alten Slots wurde beim Slot-Wechsel nicht eingeholt.");
        var startIndex = Array.FindIndex(entries, e =>
            e.Level == LogLevel.Information && e.Message.Contains("Sendeplatz 2 gestartet"));
        Assert.True(startIndex > pumpIndex,
            "Der Pump-Ende-Eintrag des alten Slots muss vor der Start-Meldung des neuen liegen.");

        await segmenter.StopAsync(ct);
    }

    [Fact]
    public async Task UnexpectedExit_AccumulatesInputFailureFlagsAcrossRestarts()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter();
        var httpUrl = "http://127.0.0.1:5002/api/internal/media-stream/" + new string('e', 64);
        await segmenter.StartAsync(FileJob(slotId: 42) with
        {
            Input = LiveStreamInput.ForHttp(httpUrl), RemainingSeconds = 3600
        }, ct);

        _processes[0].StderrPipe.WriteText("Server returned 404 Not Found\n");
        _processes[0].SimulateExit(1);
        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is { HttpTransportFailure: true });
        Assert.True(segmenter.LastUnexpectedExit!.HttpInputFailedForSlot);
        Assert.False(segmenter.LastUnexpectedExit.SftpInputFailedForSlot);

        // Retry desselben Sendeplatzes auf der sftp-Stufe — stirbt ebenfalls; der
        // Report führt die kumulierte Maske beider gescheiterten Stufen.
        await segmenter.StartAsync(FileJob(slotId: 42) with
        {
            Input = LiveStreamInput.ForSftp("sftp://user:pass@host:22/remote/movie.mkv"),
            RemainingSeconds = 3600
        }, ct);
        _processes[1].StderrPipe.WriteText("sftp://user:pass@host:22/remote/movie.mkv: Connection refused\n");
        _processes[1].SimulateExit(1);

        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is { SftpInputFailure: true });
        Assert.True(segmenter.LastUnexpectedExit!.HttpInputFailedForSlot);
        Assert.True(segmenter.LastUnexpectedExit.SftpInputFailedForSlot);
    }

    [Fact]
    public async Task UnexpectedExit_ExitWithoutStageFlags_KeepsAccumulatedSlotFlags()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter();
        var httpUrl = "http://127.0.0.1:5002/api/internal/media-stream/" + new string('f', 64);
        await segmenter.StartAsync(FileJob(slotId: 42) with
        {
            Input = LiveStreamInput.ForHttp(httpUrl), RemainingSeconds = 3600
        }, ct);

        _processes[0].StderrPipe.WriteText("Server returned 404 Not Found\n");
        _processes[0].SimulateExit(1);
        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is { HttpTransportFailure: true });

        // Der pipe:0-Retry desselben Sendeplatzes stirbt ohne Stufen-Marker — die
        // kumulierte Maske des Slots bleibt im Report erhalten.
        await segmenter.StartAsync(FileJob(slotId: 42) with
        {
            Input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream())),
            RemainingSeconds = 3600
        }, ct);
        _processes[1].SimulateExit(1);

        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is { HttpTransportFailure: false });
        Assert.True(segmenter.LastUnexpectedExit!.HttpInputFailedForSlot);
        Assert.False(segmenter.LastUnexpectedExit.SftpInputFailedForSlot);
    }

    [Fact]
    public async Task UnexpectedExit_OtherSlot_ResetsAccumulatedInputFailureFlags()
    {
        var ct = TestContext.Current.CancellationToken;
        var segmenter = CreateSegmenter();
        var httpUrl = "http://127.0.0.1:5002/api/internal/media-stream/" + new string('a', 64);
        await segmenter.StartAsync(FileJob(slotId: 42) with
        {
            Input = LiveStreamInput.ForHttp(httpUrl), RemainingSeconds = 3600
        }, ct);

        _processes[0].StderrPipe.WriteText("Server returned 404 Not Found\n");
        _processes[0].SimulateExit(1);
        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is { HttpInputFailedForSlot: true });

        // Neuer Sendeplatz — seine Fehlschlag-Maske startet leer.
        await segmenter.StartAsync(FileJob(slotId: 43) with
        {
            Input = LiveStreamInput.ForHttp(httpUrl), RemainingSeconds = 3600
        }, ct);
        _processes[1].SimulateExit(1);

        await WaitUntilAsync(() => segmenter.LastUnexpectedExit is { SlotId: 43 });
        Assert.False(segmenter.LastUnexpectedExit!.HttpInputFailedForSlot);
        Assert.False(segmenter.LastUnexpectedExit.SftpInputFailedForSlot);
    }

}
