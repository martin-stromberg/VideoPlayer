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
}
