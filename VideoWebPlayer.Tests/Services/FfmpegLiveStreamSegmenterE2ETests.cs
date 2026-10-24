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

    /// <summary>
    /// N17 regression: a second job started on top of a running one (the exact slot-change
    /// path — <see cref="FfmpegLiveStreamSegmenter.StartAsync"/> stops the old process and
    /// starts the new in one gate) must not reset the playlist. With
    /// <c>append_list</c>/<c>discont_start</c> the media playlist keeps the previous
    /// generation's segments, continues the media sequence and marks the boundary with
    /// <c>#EXT-X-DISCONTINUITY</c> — what hls.js needs to play across the transition
    /// instead of freezing on the last frame.
    /// </summary>
    [Fact]
    public async Task SecondJob_PlaylistContinuesAndMarksDiscontinuity()
    {
        var ct = TestContext.Current.CancellationToken;
        var ffmpeg = FindBinary("ffmpeg");
        if (ffmpeg is null)
            return; // kein ffmpeg auf diesem System — der Argumentpfad ist separat getestet

        var dir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-seg2-{Guid.NewGuid():N}");
        var video1 = Path.Combine(dir, "input1.mp4");
        var video2 = Path.Combine(dir, "input2.mp4");
        var segDir = Path.Combine(dir, "live");
        Directory.CreateDirectory(segDir);

        try
        {
            await GenerateVideoAsync(ffmpeg, video1, ct, 440);
            await GenerateVideoAsync(ffmpeg, video2, ct, 880);
            if (!File.Exists(video1) || !File.Exists(video2))
                return; // Videogenerierung nicht moeglich — kein verwertbarer Lauf

            var segmenter = new FfmpegLiveStreamSegmenter(NullLogger<FfmpegLiveStreamSegmenter>.Instance);
            var playlist = Path.Combine(segDir, "live.m3u8");

            await segmenter.StartAsync(Job(1, video1), ct);
            await WaitForPlaylistEntryAsync(playlist, "_1_", "erstes Segment des ersten Laufs", segmenter);

            // Sendeplatz-Wechsel: der zweite Job startet, waehrend der erste noch laeuft.
            await segmenter.StartAsync(Job(2, video2), ct);
            await WaitForPlaylistEntryAsync(playlist, "_2_", "erstes Segment des zweiten Laufs", segmenter);
            await segmenter.StopAsync(ct);

            var lines = await File.ReadAllLinesAsync(playlist, ct);
            Assert.Contains(lines, l => l.Contains("_1_")); // alte Generation bleibt gelistet
            Assert.Contains(lines, l => l.Contains("_2_"));

            // Die Grenze: eine DISCONTINUITY-Zeile, deren naechste Media-URI ein Segment
            // der neuen Generation ist — nicht nur irgendein Marker in der Playlist.
            var boundary = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("#EXT-X-DISCONTINUITY"))
                    continue;
                var nextUri = lines.Skip(i + 1).FirstOrDefault(l => !l.StartsWith('#') && !string.IsNullOrWhiteSpace(l));
                if (nextUri is not null && nextUri.Contains("_2_"))
                    boundary = i;
            }
            Assert.True(boundary >= 0,
                "Kein #EXT-X-DISCONTINUITY vor dem ersten Segment der zweiten Generation:\n"
                + string.Join('\n', lines));
            Assert.Contains(lines, l => l.StartsWith("#EXT-X-PROGRAM-DATE-TIME"));
            Assert.Null(segmenter.LastUnexpectedExit);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }

        LiveStreamJob Job(long slotId, string input) => new(
            SlotId: slotId,
            Input: LiveStreamInput.ForFile(input),
            PositionSeconds: 0,
            RemainingSeconds: 60,
            SegmentSeconds: 2,
            SegmentDirectory: segDir,
            FfmpegPath: ffmpeg);
    }

    /// <summary>Waits until the playlist lists a media URI containing <paramref name="marker"/>.</summary>
    private static async Task WaitForPlaylistEntryAsync(
        string playlist, string marker, string description, FfmpegLiveStreamSegmenter? segmenter = null)
    {
        var timeout = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < timeout)
        {
            var text = ReadPlaylistShared(playlist);
            if (text is not null
                && text.Split('\n').Any(l => l.Contains(marker) && !l.StartsWith('#')))
                return;
            await Task.Delay(100, CancellationToken.None);
        }
        Assert.Fail($"Playlist listet nach 45s kein {description} ('{marker}') "
            + $"(LastUnexpectedExit: {segmenter?.LastUnexpectedExit?.ExitCode.ToString() ?? "null"}): "
            + (ReadPlaylistShared(playlist) ?? "(Datei fehlt)"));
    }

    /// <summary>
    /// Reads the playlist while ffmpeg may be rewriting it in place — ReadWrite|Delete
    /// share, so neither side blocks the other (a FileShare.Read handle would make
    /// ffmpeg's playlist write fail and kill the muxer).
    /// </summary>
    private static string? ReadPlaylistShared(string playlist)
    {
        try
        {
            using var stream = new FileStream(playlist, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Generates a ~6s h264+aac test video via ffmpeg lavfi test sources.</summary>
    private static async Task GenerateVideoAsync(string ffmpeg, string output, CancellationToken ct, int frequency = 440)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = "-y -v error -f lavfi -i testsrc=size=320x240:rate=10 "
                + $"-f lavfi -i sine=frequency={frequency} -t 6 -c:v libx264 -preset ultrafast "
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
