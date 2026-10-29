using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="SegmenterProcessSupport.StartRealProcess"/>: a failed process start
/// returns <see langword="null"/> AND logs the underlying cause (e.g. a
/// <see cref="System.ComponentModel.Win32Exception"/> „nicht gefunden" vs. „Zugriff
/// verweigert") — the callers' context-free failure message would otherwise stay undiagnosable.
/// </summary>
public sealed class SegmenterProcessSupportTests
{
    /// <summary>
    /// <see cref="ISegmenterProcess.StandardInput"/>-Ersatz, dessen Schreibzugriff eine
    /// konfigurierbare Ausnahme wirft (Pipe-Bruch beim geplanten Stop); ohne Ausnahme
    /// werden die geschriebenen Bytes aufgezeichnet. Zählt <see cref="Stream.Close"/>-Aufrufe
    /// für den Nachweis des <c>finally</c>-Schließens.
    /// </summary>
    private sealed class ThrowingWriteStream : Stream
    {
        private readonly Exception? _writeException;
        private readonly MemoryStream _inner = new();

        public ThrowingWriteStream(Exception? writeException) => _writeException = writeException;

        public int CloseCount { get; private set; }
        public byte[] Written => _inner.ToArray();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_writeException is not null)
                throw _writeException;
            _inner.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_writeException is not null)
                throw _writeException;
            return _inner.WriteAsync(buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            CloseCount++;
            base.Dispose(disposing);
        }
    }

    /// <summary>Minimale <see cref="ISegmenterProcess"/>-Attrappe mit konfigurierbarem stdin.</summary>
    private sealed class FakePumpProcess : ISegmenterProcess
    {
        public bool HasExited { get; set; }
        public int ExitCode => HasExited ? 0 : -1;
        public Stream StandardInput { get; set; } = Stream.Null;
        public Stream StandardError { get; } = new MemoryStream();

        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Kill() { }
        public void Dispose() { }
    }

    [Fact]
    public void StartRealProcess_UnstartableBinary_LogsCauseAndReturnsNull()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());
        var missing = Path.Combine(Path.GetTempPath(), $"vwp-no-such-tool-{Guid.NewGuid():N}.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = missing,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var result = SegmenterProcessSupport.StartRealProcess(startInfo, logger, "TestCaller");

        Assert.Null(result);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("TestCaller", entry.Message);
        Assert.Contains(missing, entry.Message);
        Assert.Contains("Exception", entry.Message); // die Startursache ist mitgeloggt
    }

    [Fact]
    public async Task PumpInputAsync_BrokenPipe_LogsCompactDebugWithoutStacktrace()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());
        var stdin = new ThrowingWriteStream(new IOException("Broken pipe"));
        var process = new FakePumpProcess { StandardInput = stdin };
        var input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3])));

        var outcome = await SegmenterProcessSupport.PumpInputAsync(process, input, logger, "TestCaller");

        Assert.Equal(SegmenterPumpOutcome.Completed, outcome);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("TestCaller", entry.Message);
        Assert.Contains("Eingabe-Pump beendet", entry.Message);
        Assert.Contains("IOException", entry.Message);
        Assert.DoesNotContain("System.IO.IOException", entry.Message); // kein Ausnahme-ToString()
        Assert.Null(entry.Exception); // kein Ausnahmeobjekt → kein angehängter Stacktrace
        Assert.Equal(1, stdin.CloseCount);
    }

    public static IEnumerable<object[]> ExpectedStopExceptions()
    {
        yield return [new ObjectDisposedException("stdin-pipe", "Die Pipe ist bereits geschlossen.")];
        yield return [new OperationCanceledException("Pump abgebrochen.")];
    }

    [Theory]
    [MemberData(nameof(ExpectedStopExceptions))]
    public async Task PumpInputAsync_ExpectedStopExceptions_LogWithoutStacktrace(Exception exception)
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());
        var process = new FakePumpProcess { StandardInput = new ThrowingWriteStream(exception) };
        var input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3])));

        var outcome = await SegmenterProcessSupport.PumpInputAsync(process, input, logger, "TestCaller");

        Assert.Equal(SegmenterPumpOutcome.Completed, outcome);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("TestCaller", entry.Message);
        Assert.Contains("Eingabe-Pump beendet", entry.Message);
        Assert.Contains(exception.GetType().Name, entry.Message);
        Assert.DoesNotContain(exception.GetType().FullName!, entry.Message); // kein Ausnahme-ToString()
        Assert.Null(entry.Exception); // kein Ausnahmeobjekt → kein angehängter Stacktrace
    }

    [Fact]
    public async Task PumpInputAsync_UnexpectedError_LogsWarningWithException()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());
        var process = new FakePumpProcess
        {
            StandardInput = new ThrowingWriteStream(new InvalidOperationException("unerwartet kaputt"))
        };
        var input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3])));

        var outcome = await SegmenterProcessSupport.PumpInputAsync(process, input, logger, "TestCaller");

        Assert.Equal(SegmenterPumpOutcome.Failed, outcome);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("TestCaller", entry.Message);
        Assert.Contains("InvalidOperationException", entry.Message); // echte Fehler bleiben diagnosierbar
    }

    [Fact]
    public async Task PumpInputAsync_SourceUnavailable_ReturnsOutcomeWithoutPump()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());
        var stdin = new ThrowingWriteStream(null);
        var process = new FakePumpProcess { StandardInput = stdin };
        var input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(null));

        var outcome = await SegmenterProcessSupport.PumpInputAsync(process, input, logger, "TestCaller");

        Assert.Equal(SegmenterPumpOutcome.SourceUnavailable, outcome);
        Assert.Empty(logger.Entries); // die Warnung liegt beim Aufrufer, nicht im Helper
        Assert.Equal(1, stdin.CloseCount); // stdin wird trotzdem geschlossen
    }

    [Fact]
    public async Task PumpInputAsync_Success_PumpsAndClosesStdin()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());
        var stdin = new ThrowingWriteStream(null);
        var process = new FakePumpProcess { StandardInput = stdin };
        var input = LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3])));

        var outcome = await SegmenterProcessSupport.PumpInputAsync(process, input, logger, "TestCaller");

        Assert.Equal(SegmenterPumpOutcome.Completed, outcome);
        Assert.Equal([1, 2, 3], stdin.Written);
        Assert.Equal(1, stdin.CloseCount);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task AwaitDrainObservingFaultsAsync_CompletedTask_ReturnsTrueWithoutLog()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());

        var completed = await SegmenterProcessSupport.AwaitDrainObservingFaultsAsync(
            Task.CompletedTask, TimeSpan.FromSeconds(5), logger, "[TestCaller] Drain");

        Assert.True(completed);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task AwaitDrainObservingFaultsAsync_StalledTask_LogsWarningAndReturnsFalse()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());

        // Ein nie abschließender Task muss die Schranke auslösen — der Aufrufer wartet
        // nur begrenzt und der verwaiste Task wird im Log sichtbar.
        var completed = await SegmenterProcessSupport.AwaitDrainObservingFaultsAsync(
            new TaskCompletionSource().Task, TimeSpan.FromMilliseconds(50), logger, "[TestCaller] Drain");

        Assert.False(completed);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("[TestCaller] Drain", entry.Message);
        Assert.Contains("nicht eingeholt", entry.Message);
    }

    [Fact]
    public async Task AwaitDrainObservingFaultsAsync_FaultedTask_LogsWarningAndReturnsFalse()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());

        // Ein fehlerhafter Neben-Task darf den regulären Stop nicht gefährden — die
        // Ausnahme wird geschluckt, bleibt aber als Warnung diagnostizierbar.
        var completed = await SegmenterProcessSupport.AwaitDrainObservingFaultsAsync(
            Task.FromException(new InvalidOperationException("Drain kaputt")),
            TimeSpan.FromSeconds(5), logger, "[TestCaller] Drain");

        Assert.False(completed);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("[TestCaller] Drain", entry.Message);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task AwaitDrainObservingFaultsAsync_ResultTask_ReturnsResultOnSuccess()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());

        var result = await SegmenterProcessSupport.AwaitDrainObservingFaultsAsync(
            Task.FromResult("stderr-tail"), TimeSpan.FromSeconds(5), logger, "[TestCaller] Drain");

        Assert.Equal("stderr-tail", result);
    }

    [Fact]
    public async Task AwaitDrainObservingFaultsAsync_ResultTask_ReturnsDefaultOnTimeout()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());

        var timedOut = await SegmenterProcessSupport.AwaitDrainObservingFaultsAsync(
            new TaskCompletionSource<string>().Task, TimeSpan.FromMilliseconds(50), logger, "[TestCaller] Drain");

        Assert.Null(timedOut);
    }
}
