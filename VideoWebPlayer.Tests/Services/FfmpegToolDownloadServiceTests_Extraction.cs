using System.Net;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Traversal/allowlist proofs for <see cref="FfmpegToolDownloadService"/>: manipulated
/// archives may not write anything outside <c>tools/ffmpeg/</c> — only the two allowlisted
/// binary names land there, flat under their plain file name.
/// </summary>
public sealed class FfmpegToolDownloadServiceTests_Extraction
{
    [Fact]
    public async Task Run_ZipWithMaliciousEntries_ExtractsOnlyAllowlistBinaries()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-evil-zip", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var payload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            ("../evil", "EVIL"),
            ("../ffmpeg.exe", "EVIL-TRAVERSAL-ALLOWLIST"),
            ("subdir/../../ffmpeg.exe", "EVIL-MID-TRAVERSAL"),
            ("C:\\ffprobe.exe", "EVIL-DRIVE"),
            ("subdir/evil.exe", "EVIL-EXE"),
            ("bin/ffmpeg.exe", "FAKE-FFMPEG-BINARY"),
            ("bin/ffprobe.exe", "FAKE-FFPROBE-BINARY"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, snapshot.Status);
        var written = Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(baseDir, p).Replace('\\', '/'))
            .OrderBy(p => p)
            .ToArray();
        Assert.Equal(new[] { "tools/ffmpeg/ffmpeg.exe", "tools/ffmpeg/ffprobe.exe" }, written);
        Assert.Equal("FAKE-FFMPEG-BINARY", File.ReadAllText(Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe")));
        Assert.Empty(Directory.GetFiles(baseDir, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Run_TarXzWithMaliciousEntries_ExtractsOnlyAllowlistBinaries()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-evil-tar", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var payload = File.ReadAllBytes(
            FfmpegToolDownloadTestSupport.FixturePath("ffmpeg-malicious.tar.xz"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("linux-x64", "tar.xz"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, snapshot.Status);
        var written = Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(baseDir, p).Replace('\\', '/'))
            .OrderBy(p => p)
            .ToArray();
        Assert.Equal(new[] { "tools/ffmpeg/ffmpeg", "tools/ffmpeg/ffprobe" }, written);
        Assert.Equal("FAKE-FFMPEG-BINARY", File.ReadAllText(Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg")));
        Assert.Equal("FAKE-FFPROBE-BINARY", File.ReadAllText(Path.Combine(baseDir, "tools", "ffmpeg", "ffprobe")));
        Assert.Empty(Directory.GetFiles(baseDir, "*.tmp", SearchOption.AllDirectories));
    }
}
