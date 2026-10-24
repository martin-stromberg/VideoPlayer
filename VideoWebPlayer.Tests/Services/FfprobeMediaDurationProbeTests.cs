using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="FfprobeMediaDurationProbe"/> using the injected invocation seam —
/// no real ffprobe binary is required.
/// </summary>
public class FfprobeMediaDurationProbeTests
{
    private readonly FakeFfprobeLocator _locator = new();

    [Fact]
    public void TryParseDurationOutput_ParsesSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(123.456), FfprobeMediaDurationProbe.TryParseDurationOutput("123.456"));
        Assert.Equal(TimeSpan.FromSeconds(90), FfprobeMediaDurationProbe.TryParseDurationOutput("  90\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("N/A")]
    [InlineData("n/a")]
    [InlineData("not-a-number")]
    [InlineData("-5")]
    [InlineData("0")]
    public void TryParseDurationOutput_InvalidValues_ReturnNull(string? output)
    {
        Assert.Null(FfprobeMediaDurationProbe.TryParseDurationOutput(output));
    }

    [Fact]
    public async Task ProbeFileAsync_ParsesStdoutDuration()
    {
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
        {
            Assert.Equal("ffprobe-test", path);
            Assert.Contains("format=duration", args);
            Assert.Contains("\"video.mp4\"", args);
            Assert.Null(stdin);
            return Task.FromResult(new FfprobeInvocationResult(0, "3720.5", string.Empty, false));
        });

        var result = await probe.ProbeFileAsync("video.mp4", TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(3720.5), result.Duration);
        Assert.False(result.Transient);
    }

    [Fact]
    public async Task ProbeFileAsync_NonZeroExit_ReturnsTransientFailure()
    {
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromResult(new FfprobeInvocationResult(1, string.Empty, "error", false)));

        var result = await probe.ProbeFileAsync("video.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.True(result.Transient);
    }

    [Fact]
    public async Task ProbeFileAsync_TimedOut_ReturnsTransientFailure()
    {
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromResult(new FfprobeInvocationResult(-1, string.Empty, string.Empty, true)));

        var result = await probe.ProbeFileAsync("video.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.True(result.Transient);
    }

    [Fact]
    public async Task ProbeFileAsync_ExitZeroWithoutDuration_ReturnsPermanentFailure()
    {
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromResult(new FfprobeInvocationResult(0, "N/A", string.Empty, false)));

        var result = await probe.ProbeFileAsync("video.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.False(result.Transient);
    }

    [Fact]
    public async Task ProbeFileAsync_NoBinary_ReturnsTransientFailure()
    {
        _locator.Path = null;
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            throw new InvalidOperationException("Must not be invoked when ffprobe is missing."));

        var result = await probe.ProbeFileAsync("video.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.True(result.Transient);
    }

    [Fact]
    public async Task ProbeUriAsync_ParsesStdoutDuration()
    {
        var uri = "sftp://user:s3cret@nas.local:22/media/episode.mkv";
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
        {
            Assert.Equal("ffprobe-test", path);
            Assert.Contains($"\"{uri}\"", args);
            Assert.Contains("format=duration", args);
            Assert.Null(stdin);
            return Task.FromResult(new FfprobeInvocationResult(0, "1320.5", string.Empty, false));
        });

        var result = await probe.ProbeUriAsync(uri, TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(1320.5), result.Duration);
        Assert.False(result.Transient);
        Assert.False(result.UriUnsupported);
    }

    [Fact]
    public async Task ProbeUriAsync_UsesFileTimeout_NotStreamTimeout()
    {
        var options = Options.Create(new MediaProbeOptions { TimeoutSeconds = 7, StreamTimeoutSeconds = 42 });
        TimeSpan? captured = null;
        var probe = new FfprobeMediaDurationProbe(
            _locator,
            options,
            NullLogger<FfprobeMediaDurationProbe>.Instance,
            (path, args, stdin, timeout, ct) =>
            {
                captured = timeout;
                return Task.FromResult(new FfprobeInvocationResult(0, "10", string.Empty, false));
            });

        // Seekbarer Remote-Zugriff muss nicht die ganze Datei lesen → File-Timeout.
        await probe.ProbeUriAsync("sftp://u:p@host/media/x.mp4", TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(7), captured);
    }

    [Theory]
    [InlineData("moov atom not found")]
    [InlineData("Invalid data found when processing input")]
    public async Task ProbeUriAsync_PermanentErrorMarker_ReturnsPermanentFailure(string error)
    {
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromResult(new FfprobeInvocationResult(1, string.Empty, $"sftp://u:p@host/x.mp4: {error}", false)));

        var result = await probe.ProbeUriAsync("sftp://u:p@host/x.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.False(result.Transient);
        Assert.False(result.UriUnsupported);
    }

    [Fact]
    public async Task ProbeFileAsync_MoovAtomNotFound_ReturnsPermanentFailure()
    {
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromResult(new FfprobeInvocationResult(1, string.Empty, "video.mp4: moov atom not found", false)));

        var result = await probe.ProbeFileAsync("video.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.False(result.Transient);
    }

    [Fact]
    public async Task ProbeStreamAsync_MoovAtomNotFound_RemainsTransientFailure()
    {
        // Bei pipe:0 kann dieselbe Meldung auch durch einen vorzeitig abgebrochenen Stream
        // entstehen — nicht permanent klassifizieren.
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromResult(new FfprobeInvocationResult(1, string.Empty, "pipe:0: moov atom not found", false)));

        var result = await probe.ProbeStreamAsync(new MemoryStream(new byte[] { 1 }), TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.True(result.Transient);
    }

    [Fact]
    public async Task ProbeUriAsync_ProtocolNotFound_ReturnsUriUnsupported_AndSkipsLaterInvocations()
    {
        var calls = 0;
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
        {
            calls++;
            return Task.FromResult(new FfprobeInvocationResult(1, string.Empty, "sftp://u:p@host/x.mp4: Protocol not found", false));
        });

        var ct = TestContext.Current.CancellationToken;
        var first = await probe.ProbeUriAsync("sftp://u:p@host/x.mp4", ct);
        Assert.True(first.UriUnsupported);
        Assert.True(first.Transient);

        var second = await probe.ProbeUriAsync("sftp://u:p@host/y.mp4", ct);
        Assert.True(second.UriUnsupported);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5039/api/internal/media-stream/abc: HTTP error 404 Not Found")]
    [InlineData("http://127.0.0.1:5039/api/internal/media-stream/abc: Server returned 500 Internal Server Error")]
    [InlineData("http://127.0.0.1:5039/api/internal/media-stream/abc: Connection refused")]
    [InlineData("http://127.0.0.1:5039/api/internal/media-stream/abc: Connection timed out")]
    [InlineData("Failed to connect to 127.0.0.1 port 5039")]
    public async Task ProbeUriAsync_HttpTransportError_ReturnsUriUnsupported_AndDoesNotSuppressLaterProbes(string stderr)
    {
        var calls = 0;
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
        {
            calls++;
            return Task.FromResult(new FfprobeInvocationResult(1, string.Empty, stderr, false));
        });

        var ct = TestContext.Current.CancellationToken;
        var result = await probe.ProbeUriAsync("http://127.0.0.1:5039/api/internal/media-stream/abc", ct);
        Assert.True(result.UriUnsupported);

        // Anders als 'Protocol not found' unterdrückt ein Transportfehler keine Folge-Probes —
        // der Endpunkt könnte für das nächste Item wieder erreichbar sein.
        await probe.ProbeUriAsync("http://127.0.0.1:5039/api/internal/media-stream/def", ct);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ProbeUriAsync_TransportErrorOnNonHttpUri_RemainsTransientFailure()
    {
        // 'Connection refused' bei einer sftp://-URI ist ein read-Fehler, kein
        // HTTP-Schicht-Fehler — nicht als UriUnsupported fehlinterpretieren.
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromResult(new FfprobeInvocationResult(1, string.Empty, "sftp://u:p@host/x.mp4: Connection refused", false)));

        var result = await probe.ProbeUriAsync("sftp://u:p@host/x.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.True(result.Transient);
        Assert.False(result.UriUnsupported);
    }

    [Fact]
    public async Task ProbeUriAsync_HttpMoovError_RemainsPermanentFailure()
    {
        // Ein echtes Strukturfehler über HTTP bleibt permanent — kein Fallback nötig.
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromResult(new FfprobeInvocationResult(1, string.Empty, "http://127.0.0.1/x.mp4: moov atom not found", false)));

        var result = await probe.ProbeUriAsync("http://127.0.0.1/x.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.False(result.Transient);
        Assert.False(result.UriUnsupported);
    }

    [Fact]
    public async Task ProbeUriAsync_ErrorLogs_MaskCredentials()
    {
        var uri = "sftp://user:s3cretP4ss@nas.local:22/media/x.mp4";
        var logger = new ListLogger<FfprobeMediaDurationProbe>();
        var probe = new FfprobeMediaDurationProbe(
            _locator,
            Options.Create(new MediaProbeOptions()),
            logger,
            (path, args, stdin, timeout, ct) =>
                Task.FromResult(new FfprobeInvocationResult(1, string.Empty, $"{uri}: moov atom not found", false)));

        var result = await probe.ProbeUriAsync(uri, TestContext.Current.CancellationToken);

        Assert.False(result.Transient);
        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("s3cretP4ss"));
        Assert.Contains(logger.Messages, m => m.Contains("sftp://***@nas.local"));
    }

    [Fact]
    public async Task ProbeStreamAsync_PipesStdinWithPipeArgument()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
        {
            Assert.Contains(FfprobeMediaDurationProbe.StandardInputArgument, args);
            Assert.Same(stream, stdin);
            return Task.FromResult(new FfprobeInvocationResult(0, "60", string.Empty, false));
        });

        var result = await probe.ProbeStreamAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(60), result.Duration);
    }

    [Fact]
    public async Task ProbeFileAsync_PassesConfiguredTimeout()
    {
        var options = Options.Create(new MediaProbeOptions { TimeoutSeconds = 7 });
        TimeSpan? captured = null;
        var probe = new FfprobeMediaDurationProbe(
            _locator,
            options,
            NullLogger<FfprobeMediaDurationProbe>.Instance,
            (path, args, stdin, timeout, ct) =>
            {
                captured = timeout;
                return Task.FromResult(new FfprobeInvocationResult(0, "10", string.Empty, false));
            });

        await probe.ProbeFileAsync("video.mp4", TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(7), captured);
    }

    [Fact]
    public async Task ProbeStreamAsync_UsesStreamTimeout()
    {
        var options = Options.Create(new MediaProbeOptions { TimeoutSeconds = 7, StreamTimeoutSeconds = 42 });
        TimeSpan? captured = null;
        var probe = new FfprobeMediaDurationProbe(
            _locator,
            options,
            NullLogger<FfprobeMediaDurationProbe>.Instance,
            (path, args, stdin, timeout, ct) =>
            {
                captured = timeout;
                return Task.FromResult(new FfprobeInvocationResult(0, "10", string.Empty, false));
            });

        await probe.ProbeStreamAsync(new MemoryStream(new byte[] { 1 }), TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(42), captured);
    }

    [Fact]
    public async Task ProbeFileAsync_UnexpectedException_ReturnsTransientFailure()
    {
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            throw new IOException("Simulated process failure."));

        var result = await probe.ProbeFileAsync("video.mp4", TestContext.Current.CancellationToken);
        Assert.Null(result.Duration);
        Assert.True(result.Transient);
    }

    [Fact]
    public async Task ProbeFileAsync_Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var probe = CreateProbe((path, args, stdin, timeout, ct) =>
            Task.FromException<FfprobeInvocationResult>(new OperationCanceledException(ct)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => probe.ProbeFileAsync("video.mp4", cts.Token));
    }

    private FfprobeMediaDurationProbe CreateProbe(
        Func<string, string, Stream?, TimeSpan, CancellationToken, Task<FfprobeInvocationResult>> executeAsync)
        => new(
            _locator,
            Options.Create(new MediaProbeOptions()),
            NullLogger<FfprobeMediaDurationProbe>.Instance,
            executeAsync);

    private sealed class FakeFfprobeLocator : IFfprobeLocator
    {
        public string? Path { get; set; } = "ffprobe-test";
        public string? Resolve() => Path;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
