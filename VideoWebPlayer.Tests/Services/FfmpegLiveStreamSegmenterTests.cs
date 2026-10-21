using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="FfmpegLiveStreamSegmenter"/> with a fake process: argument building,
/// file vs. pipe input, lifecycle (start/stop/running state) and restart semantics.
/// </summary>
public sealed class FfmpegLiveStreamSegmenterTests
{
    private sealed class FakeSegmenterProcess : ISegmenterProcess
    {
        public bool HasExited { get; set; }
        public int ExitCode => HasExited ? 0 : -1;
        public Stream StandardInput { get; } = new MemoryStream();
        public Stream StandardError { get; } = new MemoryStream();
        public bool Killed { get; private set; }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            HasExited = true;
            return Task.CompletedTask;
        }

        public void Kill() { Killed = true; HasExited = true; }
        public void Dispose() { }
    }

    private readonly List<ProcessStartInfo> _started = [];
    private readonly List<FakeSegmenterProcess> _processes = [];

    private FfmpegLiveStreamSegmenter CreateSegmenter()
        => new(NullLogger<FfmpegLiveStreamSegmenter>.Instance, startInfo =>
        {
            _started.Add(startInfo);
            var process = new FakeSegmenterProcess();
            _processes.Add(process);
            return process;
        }, TimeProvider.System);

    private static LiveStreamJob FileJob(long slotId = 42)
        => new(slotId, LiveStreamInput.ForFile("/media/movie.mkv"), 125.5, 240.25, 6,
            "/segments", "/usr/bin/ffmpeg");

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
        Assert.Contains("seg_%Y%m%d%H%M%S_42_%05d.ts", args);
        Assert.Contains("-t 240.250", args);
        Assert.EndsWith("live.m3u8\"", args);
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
