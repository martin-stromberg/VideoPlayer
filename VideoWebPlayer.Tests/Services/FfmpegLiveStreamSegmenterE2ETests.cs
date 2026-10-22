using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// End-to-end coverage for <see cref="FfmpegLiveStreamSegmenter"/>: a real ffmpeg process
/// segments a real generated video file and must actually produce HLS output. The fake-process
/// unit tests only assert the argument string — this test is what would have caught invalid
/// arguments (the <c>-strftime</c>/<c>%05d</c> collision that killed the muxer with EINVAL
/// after the very first segment in production, N15). Skipped when no ffmpeg binary exists.
/// </summary>
[Trait("Category", "E2E")]
public sealed class FfmpegLiveStreamSegmenterE2ETests
{
    [Fact]
    public async Task RealFfmpeg_ProducesPlaylistAndSegments()
    {
        var ct = TestContext.Current.CancellationToken;
        var ffmpeg = FindBinary("ffmpeg");
        if (ffmpeg is null)
            return; // kein ffmpeg auf diesem System — der Argumentpfad ist separat getestet

        var dir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-seg-{Guid.NewGuid():N}");
        var video = Path.Combine(dir, "input.mp4");
        var segDir = Path.Combine(dir, "live");
        Directory.CreateDirectory(segDir);

        try
        {
            await GenerateVideoAsync(ffmpeg, video, ct);
            if (!File.Exists(video))
                return; // Videogenerierung nicht moeglich — kein verwertbarer Lauf

            var segmenter = new FfmpegLiveStreamSegmenter(NullLogger<FfmpegLiveStreamSegmenter>.Instance);
            var job = new LiveStreamJob(
                SlotId: 1,
                Input: LiveStreamInput.ForFile(video),
                PositionSeconds: 0,
                RemainingSeconds: 6,
                SegmentSeconds: 2,
                SegmentDirectory: segDir,
                FfmpegPath: ffmpeg);

            await segmenter.StartAsync(job, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            while (segmenter.IsRunning && !timeout.IsCancellationRequested)
                await Task.Delay(100, CancellationToken.None);

            var playlist = Path.Combine(segDir, "live.m3u8");
            Assert.True(File.Exists(playlist), $"Playlist wurde nicht erzeugt (Exit: {segmenter.LastUnexpectedExit?.ExitCode})");
            Assert.Contains("#EXTM3U", await File.ReadAllTextAsync(playlist, ct));
            Assert.NotEmpty(Directory.GetFiles(segDir, "seg_*.ts"));
            Assert.Null(segmenter.LastUnexpectedExit);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    /// <summary>Generates a ~6s h264+aac test video via ffmpeg lavfi test sources.</summary>
    private static async Task GenerateVideoAsync(string ffmpeg, string output, CancellationToken ct)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = "-y -v error -f lavfi -i testsrc=size=320x240:rate=10 "
                + "-f lavfi -i sine=frequency=440 -t 6 -c:v libx264 -preset ultrafast "
                + "-pix_fmt yuv420p -c:a aac -shortest \"" + output + "\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        await process.WaitForExitAsync(ct);
    }

    private static string? FindBinary(string name)
    {
        var exeName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
