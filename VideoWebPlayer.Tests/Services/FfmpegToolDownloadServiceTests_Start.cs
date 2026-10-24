using System.Net;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Start-time behaviour of <see cref="FfmpegToolDownloadService"/>: the server-side admin
/// check and the parallel-start lock.
/// </summary>
public sealed class FfmpegToolDownloadServiceTests_Start
{
    [Fact]
    public async Task StartDownloadAsync_NonAdmin_Throws()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-nonadmin", ct);
        var user = await fixture.AddUserAsync("user@test.com", isAdmin: false);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(Array.Empty<byte>()), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.StartDownloadAsync(user.Id, ct));
        Assert.Equal(FfmpegToolDownloadJobStatus.Idle, service.GetSnapshot().Status);
    }

    [Fact]
    public async Task StartDownloadAsync_AnonymousUser_Throws()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-anonymous", ct);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(Array.Empty<byte>()), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.StartDownloadAsync("no-such-user", ct));
        Assert.Equal(FfmpegToolDownloadJobStatus.Idle, service.GetSnapshot().Status);
    }

    [Fact]
    public async Task StartDownloadAsync_WhileRunning_ReturnsNotStarted()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-parallel", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();

        // Slow payload keeps the first job in Running while the second start is attempted.
        var payload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            ("pkg/bin/ffmpeg.exe", "FAKE-FFMPEG"),
            ("pkg/bin/ffprobe.exe", "FAKE-FFPROBE"));
        var handler = new ContentFactoryHandler(() =>
            new StreamContent(new ThrottledStream(payload, chunkSize: 4, delayMilliseconds: 60)));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, handler, baseDir, platformSelector: () => ("win-x64", "zip"));

        var first = await service.StartDownloadAsync(admin.Id, ct);
        Assert.True(first.Started);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!service.GetSnapshot().IsActive && DateTime.UtcNow < deadline)
            await Task.Delay(10, ct);

        var second = await service.StartDownloadAsync(admin.Id, ct);
        Assert.False(second.Started);
        Assert.Equal(first.Snapshot.Id, second.Snapshot.Id);

        var final = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);
        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, final.Status);
    }

    [Fact]
    public async Task AcknowledgeTerminalState_ClearsFinishedSnapshot()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-ack", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(Array.Empty<byte>(), HttpStatusCode.NotFound), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var failed = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);
        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, failed.Status);

        service.AcknowledgeTerminalState(failed.Id);

        Assert.Equal(FfmpegToolDownloadJobStatus.Idle, service.GetSnapshot().Status);
    }

    [Fact]
    public async Task AcknowledgeTerminalState_StaleId_KeepsSnapshot()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-ack-stale", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, new BytesHandler(Array.Empty<byte>(), HttpStatusCode.NotFound), baseDir,
            platformSelector: () => ("win-x64", "zip"));

        await service.StartDownloadAsync(admin.Id, ct);
        var failed = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);
        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, failed.Status);

        // A late acknowledge naming a snapshot the caller once saw must not erase the
        // current terminal state — otherwise a never-displayed result silently vanishes.
        service.AcknowledgeTerminalState(Guid.NewGuid());

        var kept = service.GetSnapshot();
        Assert.Equal(FfmpegToolDownloadJobStatus.Failed, kept.Status);
        Assert.Equal(failed.Id, kept.Id);

        // Sanity: the matching id still acknowledges normally.
        service.AcknowledgeTerminalState(failed.Id);
        Assert.Equal(FfmpegToolDownloadJobStatus.Idle, service.GetSnapshot().Status);
    }

    [Fact]
    public async Task AcknowledgeTerminalState_KeepsActiveJob()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ffmpegdl-ack-active", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var baseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();

        // Slow payload keeps the job active while the acknowledge call arrives — an
        // acknowledge raced against a running job must never erase its progress.
        var payload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            ("pkg/bin/ffmpeg.exe", "FAKE-FFMPEG"),
            ("pkg/bin/ffprobe.exe", "FAKE-FFPROBE"));
        var handler = new ContentFactoryHandler(() =>
            new StreamContent(new ThrottledStream(payload, chunkSize: 4, delayMilliseconds: 60)));
        var service = FfmpegToolDownloadTestSupport.CreateService(
            fixture.Db, handler, baseDir, platformSelector: () => ("win-x64", "zip"));

        var started = await service.StartDownloadAsync(admin.Id, ct);
        Assert.True(started.Started);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!service.GetSnapshot().IsActive && DateTime.UtcNow < deadline)
            await Task.Delay(10, ct);

        service.AcknowledgeTerminalState(started.Snapshot.Id);

        var snapshot = service.GetSnapshot();
        Assert.True(snapshot.IsActive);
        Assert.Equal(started.Snapshot.Id, snapshot.Id);

        var final = await FfmpegToolDownloadTestSupport.AwaitCompletionAsync(service);
        Assert.Equal(FfmpegToolDownloadJobStatus.Succeeded, final.Status);
    }
}
