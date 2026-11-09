using System.IO.Compression;
using System.Net;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Failure paths of <see cref="FfmpegToolDownloadService"/>: HTTP errors, corrupt or
/// incomplete archives, unsupported platforms and an unwritable target directory end in a
/// <c>Failed</c> snapshot with a comprehensible message and cleaned-up temp files.
/// </summary>
public sealed class FfmpegToolDownloadServiceTests_Failures
{
    [Fact]
    public async Task Run_HttpError_Fails()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-http404", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(Array.Empty<byte>(), HttpStatusCode.NotFound), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Contains("nicht heruntergeladen werden", snapshot.Error);
        // Kein Technik-Token fuer die UI — der Statuscode bleibt ein Diagnosedetail im Server-Log.
        Assert.DoesNotContain("404", snapshot.Error);
        AssertTempFileRemoved(snapshot);
    }

    [Fact]
    public async Task Run_CorruptArchive_Fails()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-corrupt", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler("this is not a zip file"u8.ToArray()), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Contains("entpackt", snapshot.Error);
        AssertTempFileRemoved(snapshot);
        Assert.Empty(Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Run_MissingBinaryInArchive_Fails()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-missing", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var payload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            ("pkg/bin/ffmpeg.exe", "FAKE-FFMPEG"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Contains("Paket enthält kein", snapshot.Error);
        Assert.Contains("ffprobe", snapshot.Error);
        Assert.False(File.Exists(Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe")));
        AssertTempFileRemoved(snapshot);
        Assert.Empty(Directory.GetFiles(baseDir, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Run_UnsupportedPlatform_Fails()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-platform", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(Array.Empty<byte>()), baseDir,
            platformSelector: () => null);

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Contains("System wird nicht unterstützt", snapshot.Error);
        Assert.Contains("manuell installieren", snapshot.Error);
        // Ein "eigener Pfad in den Einstellungen" existiert nicht in der UI — der Text
        // muss auf die Server-Konfiguration verweisen.
        Assert.Contains("Server-Konfiguration", snapshot.Error);
        Assert.DoesNotContain("in den Einstellungen", snapshot.Error);
    }

    [Fact]
    public async Task Run_TargetDirectoryNotWritable_Fails()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-nowrite", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();

        // Deterministic write-permission failure: {baseDir}/tools exists as a FILE, so
        // tools/ffmpeg/ can never be created — no ACL manipulation needed.
        await File.WriteAllTextAsync(Path.Combine(baseDir, "tools"), "not a directory", ct);

        var payload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            ("pkg/bin/ffmpeg.exe", "FAKE-FFMPEG"),
            ("pkg/bin/ffprobe.exe", "FAKE-FFPROBE"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Contains("Schreibrechte", snapshot.Error);
        // Der betroffene Ordner wird beim Namen genannt.
        Assert.Contains("tools/ffmpeg/", snapshot.Error);
    }

    [Fact]
    public async Task Run_StalledDownload_FailsAfterStallTimeout()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-stall", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();

        // The server answers headers + a first chunk and then never sends again: without
        // the stall watchdog the copy loop would hang in Running forever and block every
        // further download via the IsActive lock until a restart.
        var handler = new ContentFactoryHandler(() =>
            new StreamContent(new StalledStream("PARTIAL-DATA"u8.ToArray())));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, handler, baseDir,
            platformSelector: () => ("win-x64", "zip"),
            downloadStallTimeout: TimeSpan.FromMilliseconds(300));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(
            service, timeoutMilliseconds: 15_000);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Contains("keine Daten", snapshot.Error);
        AssertTempFileRemoved(snapshot);
    }

    [Fact]
    public async Task Run_NetworkError_ShowsComprehensibleMessage()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-neterr", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db,
            new ExceptionHandler(() => new HttpRequestException("No such host is known.")),
            baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Contains("nicht heruntergeladen werden", snapshot.Error);
        Assert.Contains("Internetverbindung", snapshot.Error);
        Assert.DoesNotContain("No such host", snapshot.Error);
    }

    [Fact]
    public async Task Run_UnexpectedError_ShowsGenericMessage()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-generr", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(Array.Empty<byte>()), baseDir,
            platformSelector: () => throw new Exception("internal RID detection detail"));

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Equal("Der Download ist fehlgeschlagen. Details stehen im Server-Protokoll.", snapshot.Error);
    }

    [Fact]
    public async Task Run_OversizedArchiveEntry_FailsAndLeavesNoTmpFiles()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-oversize", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();

        // Doubles as the regression for mid-copy failures: the partially written *.tmp was
        // registered only after a successful copy, so an abort inside the copy loop left it
        // behind; the finally-cleanup must remove it.
        var payload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            CompressionLevel.NoCompression,
            ("pkg/bin/ffmpeg.exe", new string('a', 4096)),
            ("pkg/bin/ffprobe.exe", "FAKE-FFPROBE"));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(payload), baseDir,
            platformSelector: () => ("win-x64", "zip"),
            maxExtractedEntryBytes: 1024);

        await service.StartDownloadAsync(admin.Id, ct);
        var snapshot = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);

        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, snapshot.Status);
        Assert.Contains("unerwartet große Datei", snapshot.Error);
        Assert.Empty(Directory.GetFiles(baseDir, "*.tmp", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe")));
        Assert.False(File.Exists(Path.Combine(baseDir, "tools", "ffmpeg", "ffprobe.exe")));
        AssertTempFileRemoved(snapshot);
    }

    private static void AssertTempFileRemoved(FfmpegToolDownloadJobSnapshot snapshot)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ffmpeg-tools-{snapshot.Id:N}.download");
        Assert.False(File.Exists(tempFile));
    }
}
