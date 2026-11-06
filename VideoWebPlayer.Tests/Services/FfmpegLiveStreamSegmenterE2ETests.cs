using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
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
        var ffmpeg = E2EBinaryLocator.Find("ffmpeg");
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
        var ffmpeg = E2EBinaryLocator.Find("ffmpeg");
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

    /// <summary>
    /// The scenario from the production log: a real ffmpeg process fed through
    /// <c>pipe:0</c> is stopped deliberately while its stdin pump is still blocked by the
    /// <c>-re</c> backpressure. The kill breaks the pipe — the pump's end must surface as a
    /// compact debug entry without an exception stack trace (the previously logged
    /// „System.IO.IOException: Broken pipe" with full trace looked like an exception error).
    /// </summary>
    [Fact]
    public async Task RealFfmpeg_PipeInputStop_LogsNoExceptionStacktrace()
    {
        var ffmpeg = E2EBinaryLocator.Find("ffmpeg");
        if (ffmpeg is null)
            return; // kein ffmpeg auf diesem System

        var dir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-pipestop-{Guid.NewGuid():N}");
        // MPEG-TS ist aus einer nicht-seekbaren Pipe lesbar (MP4 scheiterte hier am
        // moov-Atom am Dateiende — ffmpeg beendet sich dann vor dem Stop).
        var video = Path.Combine(dir, "input.ts");
        var segDir = Path.Combine(dir, "live");
        Directory.CreateDirectory(segDir);

        var ct = TestContext.Current.CancellationToken;
        try
        {
            await GenerateVideoAsync(ffmpeg, video, ct);
            if (!File.Exists(video))
                return; // Videogenerierung nicht moeglich — kein verwertbarer Lauf

            var logger = new ListLogger<FfmpegLiveStreamSegmenter>(new ConcurrentQueue<string>());
            var segmenter = new FfmpegLiveStreamSegmenter(logger);
            var job = new LiveStreamJob(
                SlotId: 77,
                Input: LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(
                    new ThrottledReadStream(
                        new FileStream(video, FileMode.Open, FileAccess.Read, FileShare.Read)))),
                PositionSeconds: 0,
                RemainingSeconds: 60,
                SegmentSeconds: 2,
                SegmentDirectory: segDir,
                FfmpegPath: ffmpeg);

            await segmenter.StartAsync(job, ct);
            // ~1s laufen lassen: die gedrosselte Quelle ist beim Stop noch am Pumpen;
            // der Kill bricht die Pipe zwingend.
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            await segmenter.StopAsync(CancellationToken.None);

            var entries = logger.Entries.ToArray();
            var pumpEntry = Assert.Single(entries, e =>
                e.Level == LogLevel.Debug && e.Message.Contains("Slot 77")
                && e.Message.Contains("Eingabe-Pump beendet"));
            Assert.DoesNotContain("System.IO.IOException", pumpEntry.Message);
            Assert.Null(pumpEntry.Exception); // kein Ausnahmeobjekt → kein angehängter Stacktrace
            Assert.DoesNotContain(entries, e => e.Exception is not null); // nirgends ein Stacktrace
            Assert.DoesNotContain(entries, e =>
                e.Level >= LogLevel.Warning && e.Message.Contains("Pump"));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// The production failure of Issue #311: an MP4 whose moov atom sits at the end fed
    /// through <c>pipe:0</c> — ffmpeg dies on its own with demux markers, which the
    /// segmenter must classify as <see cref="LiveStreamExitInfo.UndecodableInput"/> so the
    /// coordinator skips the slot instead of restart-looping.
    /// </summary>
    [Fact]
    public async Task RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput()
    {
        var ct = TestContext.Current.CancellationToken;
        var ffmpeg = E2EBinaryLocator.Find("ffmpeg");
        if (ffmpeg is null)
            return;

        var dir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-moov-{Guid.NewGuid():N}");
        var video = Path.Combine(dir, "input.mp4"); // lavfi-mp4 ohne faststart: moov am Ende
        var segDir = Path.Combine(dir, "live");
        Directory.CreateDirectory(segDir);

        try
        {
            await GenerateVideoAsync(ffmpeg, video, ct);
            if (!File.Exists(video))
                return;

            var segmenter = new FfmpegLiveStreamSegmenter(NullLogger<FfmpegLiveStreamSegmenter>.Instance);
            var job = new LiveStreamJob(
                SlotId: 55,
                Input: LiveStreamInput.ForStream(_ => Task.FromResult<Stream?>(
                    new FileStream(video, FileMode.Open, FileAccess.Read, FileShare.Read))),
                PositionSeconds: 0,
                RemainingSeconds: 60,
                SegmentSeconds: 2,
                SegmentDirectory: segDir,
                FfmpegPath: ffmpeg);

            await segmenter.StartAsync(job, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            while (segmenter.LastUnexpectedExit is null && !timeout.IsCancellationRequested)
                await Task.Delay(200, CancellationToken.None);

            Assert.NotNull(segmenter.LastUnexpectedExit);
            Assert.True(segmenter.LastUnexpectedExit.UndecodableInput,
                $"Erwartet: UndecodableInput — tatsächlich: {segmenter.LastUnexpectedExit}");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// The counterpart: the same moov-at-end MP4 as a seekable file input with a start
    /// offset (<c>-ss</c>) — the actual fixed path — must produce segments.
    /// </summary>
    [Fact]
    public async Task RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments()
    {
        var ct = TestContext.Current.CancellationToken;
        var ffmpeg = E2EBinaryLocator.Find("ffmpeg");
        if (ffmpeg is null)
            return;

        var dir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-moovfile-{Guid.NewGuid():N}");
        var video = Path.Combine(dir, "input.mp4");
        var segDir = Path.Combine(dir, "live");
        Directory.CreateDirectory(segDir);

        try
        {
            await GenerateVideoAsync(ffmpeg, video, ct);
            if (!File.Exists(video))
                return;

            var segmenter = new FfmpegLiveStreamSegmenter(NullLogger<FfmpegLiveStreamSegmenter>.Instance);
            var job = new LiveStreamJob(
                SlotId: 56,
                Input: LiveStreamInput.ForFile(video),
                PositionSeconds: 2,
                RemainingSeconds: 4,
                SegmentSeconds: 2,
                SegmentDirectory: segDir,
                FfmpegPath: ffmpeg);

            await segmenter.StartAsync(job, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            while (segmenter.IsRunning && !timeout.IsCancellationRequested)
                await Task.Delay(100, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(segDir, "live.m3u8")),
                $"Playlist wurde nicht erzeugt (Exit: {segmenter.LastUnexpectedExit?.ExitCode})");
            Assert.NotEmpty(Directory.GetFiles(segDir, "seg_*.ts"));
            Assert.Null(segmenter.LastUnexpectedExit);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// An <c>sftp://</c> input against an unreachable port must die fast and classify as
    /// <see cref="LiveStreamExitInfo.SftpInputFailure"/> — either a transport failure
    /// (libssh present) or an unsupported scheme (build without libssh, which also latches
    /// <see cref="FfmpegLiveStreamSegmenter.SftpInputSupported"/>).
    /// </summary>
    [Fact]
    public async Task RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var ffmpeg = E2EBinaryLocator.Find("ffmpeg");
        if (ffmpeg is null)
            return;

        var segDir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-sftp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(segDir);

        try
        {
            var segmenter = new FfmpegLiveStreamSegmenter(NullLogger<FfmpegLiveStreamSegmenter>.Instance);
            var job = new LiveStreamJob(
                SlotId: 57,
                Input: LiveStreamInput.ForSftp("sftp://user:pass@127.0.0.1:1/remote/input.mp4"),
                PositionSeconds: 0,
                RemainingSeconds: 60,
                SegmentSeconds: 2,
                SegmentDirectory: segDir,
                FfmpegPath: ffmpeg);

            await segmenter.StartAsync(job, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            while (segmenter.LastUnexpectedExit is null && !timeout.IsCancellationRequested)
                await Task.Delay(200, CancellationToken.None);

            Assert.NotNull(segmenter.LastUnexpectedExit);
            Assert.True(segmenter.LastUnexpectedExit.SftpInputFailure,
                $"Erwartet: SftpInputFailure — tatsächlich: {segmenter.LastUnexpectedExit}");
        }
        finally
        {
            try { Directory.Delete(segDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// FileStream-Hülle, die Lesezugriffe drosselt (~33 KB/s), damit die Eingabe-Pumpe
    /// beim geplanten Stop noch aktiv ist — ffmpeg entleert die Pipe sonst schneller,
    /// als die kurze Testdatei gepumpt werden kann, und der Pipe-Bruch fiele nie an.
    /// </summary>
    private sealed class ThrottledReadStream : Stream
    {
        private readonly Stream _inner;

        public ThrottledReadStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer[..Math.Min(buffer.Length, 1024)], cancellationToken);
            if (read > 0)
                await Task.Delay(30, CancellationToken.None);
            return read;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
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
}
