using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Happy-path installation behaviour of <see cref="FfmpegToolDownloadService"/>: zip and
/// tar.xz packages land flat under <c>tools/ffmpeg/</c>, progress is reported, Unix
/// binaries get the executable bit and the locators find the binaries afterwards.
/// </summary>
public sealed class FfmpegToolDownloadServiceTests_Install
{
    [Fact]
    public async Task Run_ZipArchive_InstallsBothBinariesToToolsFfmpeg()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-zip", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var payload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            ("ffmpeg-8.0-essentials_build/bin/ffmpeg.exe", "FAKE-FFMPEG-BINARY"),
            ("ffmpeg-8.0-essentials_build/bin/ffprobe.exe", "FAKE-FFPROBE-BINARY"),
            ("ffmpeg-8.0-essentials_build/bin/ffplay.exe", "NOT-NEEDED"),
            ("ffmpeg-8.0-essentials_build/README.txt", "docs"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        var start = await service.StartDownloadAsync(admin.Id, ct);
        Assert.True(start.Started);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, snapshot.Status);
        var ffmpeg = Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe");
        var ffprobe = Path.Combine(baseDir, "tools", "ffmpeg", "ffprobe.exe");
        Assert.True(File.Exists(ffmpeg));
        Assert.True(File.Exists(ffprobe));
        Assert.Equal("FAKE-FFMPEG-BINARY", File.ReadAllText(ffmpeg));
        Assert.Equal("FAKE-FFPROBE-BINARY", File.ReadAllText(ffprobe));
        Assert.False(File.Exists(Path.Combine(baseDir, "tools", "ffmpeg", "ffplay.exe")));
        Assert.Empty(Directory.GetFiles(baseDir, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Run_TarXzArchive_InstallsBothBinaries()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-tarxz", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var payload = File.ReadAllBytes(
            FfmpegToolDownloadTestSupport.FixturePath("ffmpeg-release-amd64-static.tar.xz"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("linux-x64", "tar.xz"));

        var start = await service.StartDownloadAsync(admin.Id, ct);
        Assert.True(start.Started);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, snapshot.Status);
        var ffmpeg = Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg");
        var ffprobe = Path.Combine(baseDir, "tools", "ffmpeg", "ffprobe");
        Assert.True(File.Exists(ffmpeg));
        Assert.True(File.Exists(ffprobe));
        Assert.Equal("FAKE-FFMPEG-BINARY", File.ReadAllText(ffmpeg));
        Assert.Equal("FAKE-FFPROBE-BINARY", File.ReadAllText(ffprobe));
        Assert.Empty(Directory.GetFiles(baseDir, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Run_OnUnix_SetsExecutableBit()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(),
            "Unix-Dateirechte lassen sich nur unter Unix prüfen.");

        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-execbit", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var payload = File.ReadAllBytes(
            FfmpegToolDownloadTestSupport.FixturePath("ffmpeg-release-amd64-static.tar.xz"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("linux-x64", "tar.xz"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, snapshot.Status);
        foreach (var name in new[] { "ffmpeg", "ffprobe" })
        {
            var mode = File.GetUnixFileMode(Path.Combine(baseDir, "tools", "ffmpeg", name));
            Assert.True(mode.HasFlag(UnixFileMode.UserExecute), $"{name}: UserExecute fehlt ({mode})");
            Assert.True(mode.HasFlag(UnixFileMode.GroupExecute), $"{name}: GroupExecute fehlt ({mode})");
            Assert.True(mode.HasFlag(UnixFileMode.OtherExecute), $"{name}: OtherExecute fehlt ({mode})");
        }
    }

    [Fact]
    public async Task Run_ReportsProgress()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-progress", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var payload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            CompressionLevel.NoCompression,
            ("pkg/bin/ffmpeg.exe", new string('a', 200_000)),
            ("pkg/bin/ffprobe.exe", new string('b', 200_000)));

        var samples = new List<long>();
        FfmpegToolDownloadService? service = null;
        var handler = new ContentFactoryHandler(() =>
        {
            // StreamContent meldet ohne seekbaren Stream kein Content-Length selbst.
            var content = new StreamContent(new ThrottledStream(
                payload, chunkSize: 40_000, delayMilliseconds: 80,
                onChunk: () =>
                {
                    var received = service!.GetSnapshot().BytesReceived;
                    if (samples.Count == 0 || samples[^1] != received)
                        samples.Add(received);
                }));
            content.Headers.ContentLength = payload.Length;
            return content;
        });
        service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, handler, baseDir, platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, snapshot.Status);
        Assert.Equal(payload.Length, snapshot.TotalBytes);
        Assert.Equal(payload.Length, snapshot.BytesReceived);
        var intermediate = samples.Where(s => s > 0 && s < payload.Length).ToList();
        Assert.NotEmpty(intermediate);
        Assert.True(intermediate.SequenceEqual(intermediate.Order()), "BytesReceived muss wachsen");
    }

    [Fact]
    public async Task AfterInstall_LocatorResolveFindsBinaries()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-locators", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var payload = File.ReadAllBytes(
            FfmpegToolDownloadTestSupport.FixturePath("ffmpeg-release-amd64-static.tar.xz"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("linux-x64", "tar.xz"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);
        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, snapshot.Status);

        var options = Options.Create(new MediaProbeOptions());
        var ffprobeLocator = new FfprobeLocator(options, baseDir, () => null);
        var ffmpegLocator = new FfmpegLocator(options, baseDir, () => null);

        // „ffprobe"/„ffmpeg" ohne Endung loesen auf Windows und Linux gleichermassen auf —
        // die Windows-Kandidatenliste enthaelt den endungslosen Namen als Fallback.
        Assert.Equal(Path.Combine(baseDir, "tools", "ffmpeg", "ffprobe"), ffprobeLocator.Resolve());
        Assert.Equal(Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg"), ffmpegLocator.Resolve());
    }

    [Fact]
    public async Task Run_ConfiguredPaths_RemainUntouched()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-cfgpaths", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();

        var markerDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var ffprobeMarker = Path.Combine(markerDir, "my-ffprobe");
        var ffmpegMarker = Path.Combine(markerDir, "my-ffmpeg");
        await File.WriteAllTextAsync(ffprobeMarker, "MARKER-FFPROBE", ct);
        await File.WriteAllTextAsync(ffmpegMarker, "MARKER-FFMPEG", ct);

        var options = new MediaProbeOptions { FfprobePath = ffprobeMarker, FfmpegPath = ffmpegMarker };
        var payload = File.ReadAllBytes(
            FfmpegToolDownloadTestSupport.FixturePath("ffmpeg-release-amd64-static.tar.xz"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir, options,
            platformSelector: () => ("linux-x64", "tar.xz"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, snapshot.Status);
        Assert.Equal("MARKER-FFPROBE", File.ReadAllText(ffprobeMarker));
        Assert.Equal("MARKER-FFMPEG", File.ReadAllText(ffmpegMarker));

        var resolveOptions = Options.Create(options);
        Assert.Equal(ffprobeMarker, new FfprobeLocator(resolveOptions, baseDir, () => null).Resolve());
        Assert.Equal(ffmpegMarker, new FfmpegLocator(resolveOptions, baseDir, () => null).Resolve());

        var written = Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(baseDir, p).Replace('\\', '/'))
            .OrderBy(p => p)
            .ToArray();
        Assert.Equal(new[] { "tools/ffmpeg/ffmpeg", "tools/ffmpeg/ffprobe" }, written);
    }
}
