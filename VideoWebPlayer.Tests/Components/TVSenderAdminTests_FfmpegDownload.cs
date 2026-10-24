using Bunit;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the ffmpeg/ffprobe download feature of <see cref="TVSenderAdmin"/>:
/// the "binary missing" notices with their download buttons, the GPL confirmation dialog
/// and the progress/error display.
/// </summary>
public sealed class TVSenderAdminTests_FfmpegDownload
{
    [Fact]
    public async Task FfmpegMissing_ShowsNoticeAndDownloadButton()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-ffmpeg-missing", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffmpegLocator: new AbsentFfmpegLocator());
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() =>
        {
            var notice = cut.Find("#tvsender-ffmpeg-missing");
            Assert.Contains("Live-Stream pausiert", notice.TextContent);
            Assert.NotNull(notice.QuerySelector("#tvsender-download-tools-ffmpeg"));
        }, TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll("#tvsender-ffprobe-missing"));
    }

    [Fact]
    public async Task FfprobeMissing_ShowsNoticeAndDownloadButton()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-ffprobe-missing", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator());
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() =>
        {
            var notice = cut.Find("#tvsender-ffprobe-missing");
            Assert.Contains("ffprobe wurde nicht gefunden", notice.TextContent);
            Assert.NotNull(notice.QuerySelector("#tvsender-download-tools-ffprobe"));
        }, TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll("#tvsender-ffmpeg-missing"));
    }

    [Fact]
    public async Task BinariesPresent_NoNotice()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-tools-present", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-laufzeit"), TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll("#tvsender-ffprobe-missing"));
        Assert.Empty(cut.FindAll("#tvsender-ffmpeg-missing"));
        Assert.Empty(cut.FindAll("#tvsender-download-tools-ffprobe"));
        Assert.Empty(cut.FindAll("#tvsender-download-tools-ffmpeg"));
    }

    [Fact]
    public async Task DownloadButton_OpensGplDialog()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-gpl-dialog", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var downloadService = new FakeFfmpegToolDownloadService();
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator(),
            downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-download-tools-ffprobe"), TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll("#tvsender-download-dialog"));
        cut.Find("#tvsender-download-tools-ffprobe").Click();

        var dialog = cut.Find("#tvsender-download-dialog");
        Assert.NotNull(dialog.Closest(".admin-dialog-overlay"));
        Assert.Contains("--enable-gpl", dialog.TextContent);
        Assert.Equal(0, downloadService.StartCalls);
    }

    [Fact]
    public async Task GplDialog_Confirm_StartsDownload()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-gpl-confirm", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var downloadService = new FakeFfmpegToolDownloadService();
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator(),
            ffmpegLocator: new AbsentFfmpegLocator(), downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-download-tools-ffprobe"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-download-tools-ffprobe").Click();
        cut.Find("#tvsender-download-confirm").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, downloadService.StartCalls);
            Assert.Equal(admin.Id, downloadService.ReceivedUserId);
            Assert.Empty(cut.FindAll("#tvsender-download-dialog"));
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task DownloadFailed_ShowsErrorAlert_NoticeRemains()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-dl-failed", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var downloadService = new FakeFfmpegToolDownloadService(new FfmpegToolDownloadJobSnapshot(
            Guid.NewGuid(), FfmpegToolDownloadJobStatus.Failed,
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow,
            "Download fehlgeschlagen.",
            "Das Paket konnte nicht heruntergeladen werden. Bitte die Internetverbindung prüfen und den Download erneut starten.",
            0, 1024));
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator(),
            downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() =>
        {
            var error = cut.Find("#tvsender-download-error");
            Assert.Contains("nicht heruntergeladen werden", error.TextContent);
            Assert.Contains("alert-danger", error.ClassList);
            cut.Find("#tvsender-ffprobe-missing");
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task DownloadFailed_ErrorAlertIsDismissible()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-dl-errclose", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var jobId = Guid.NewGuid();
        var downloadService = new FakeFfmpegToolDownloadService(new FfmpegToolDownloadJobSnapshot(
            jobId, FfmpegToolDownloadJobStatus.Failed,
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow,
            "Download fehlgeschlagen.", "Der Download ist fehlgeschlagen. Details stehen im Server-Protokoll.",
            0, 1024));
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator(),
            downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-download-error"), TimeSpan.FromSeconds(10));

        cut.Find("#tvsender-download-error-close").Click();

        Assert.Empty(cut.FindAll("#tvsender-download-error"));
        // Schließen quittiert genau den angezeigten Endstand im Service: die Init hatte
        // ihn schon einmal quittiert, der Klick liefert die zweite Bestätigung derselben Id.
        Assert.Equal(2, downloadService.AcknowledgedIds.Count(id => id == jobId));
    }

    [Fact]
    public async Task TerminalSnapshot_DoesNotResurface_OnNextVisit()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-dl-noRevisit", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var jobId = Guid.NewGuid();
        var downloadService = new FakeFfmpegToolDownloadService(new FfmpegToolDownloadJobSnapshot(
            jobId, FfmpegToolDownloadJobStatus.Failed,
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow,
            "Download fehlgeschlagen.", "Der Download ist fehlgeschlagen. Details stehen im Server-Protokoll.",
            0, 1024));
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator(),
            downloadService: downloadService);
        using var _ctx = ctx;

        // Erste Komponente (erster Beobachter): sieht den Endstand genau einmal und
        // quittiert genau diesen Stand im Service.
        var first = ctx.Render<TVSenderAdmin>();
        first.WaitForAssertion(() => first.Find("#tvsender-download-error"), TimeSpan.FromSeconds(10));
        Assert.Contains(jobId, downloadService.AcknowledgedIds);
        first.Dispose();

        // Zweiter Seitenaufruf (neue Komponenteninstanz, gleicher Service): der quittierte
        // Endstand darf nicht erneut als aktueller Hinweis aufpoppen.
        var second = ctx.Render<TVSenderAdmin>();
        second.WaitForAssertion(() => second.Find("#tvsender-tab-regeln"), TimeSpan.FromSeconds(10));
        Assert.Empty(second.FindAll("#tvsender-download-error"));
        Assert.Empty(second.FindAll("#tvsender-download-success"));
    }

    [Fact]
    public async Task DownloadActive_DisablesButtons()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-dl-active", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var downloadService = new FakeFfmpegToolDownloadService(new FfmpegToolDownloadJobSnapshot(
            Guid.NewGuid(), FfmpegToolDownloadJobStatus.Running,
            DateTimeOffset.UtcNow, null, "ffmpeg/ffprobe werden heruntergeladen.", null,
            512, 1024));
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator(),
            ffmpegLocator: new AbsentFfmpegLocator(), downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("#tvsender-download-tools-ffprobe").HasAttribute("disabled"));
            Assert.True(cut.Find("#tvsender-download-tools-ffmpeg").HasAttribute("disabled"));
            cut.Find("#tvsender-download-progress-card");
        }, TimeSpan.FromSeconds(10));

        // ARIA: a determinate bar reports percent, not raw bytes (512/1024 = 50 %).
        var bar = cut.Find("#tvsender-download-progress-card .progress");
        Assert.Equal("50", bar.GetAttribute("aria-valuenow"));
        Assert.Equal("0", bar.GetAttribute("aria-valuemin"));
        Assert.Equal("100", bar.GetAttribute("aria-valuemax"));
    }

    [Fact]
    public async Task DownloadActive_UnknownTotal_ProgressbarIsIndeterminate()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-dl-nototal", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var downloadService = new FakeFfmpegToolDownloadService(new FfmpegToolDownloadJobSnapshot(
            Guid.NewGuid(), FfmpegToolDownloadJobStatus.Running,
            DateTimeOffset.UtcNow, null, "ffmpeg/ffprobe werden heruntergeladen.", null,
            512 * 1024, null));
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator(),
            downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() =>
        {
            var bar = cut.Find("#tvsender-download-progress-card .progress");
            // Without a known total the bar must not report valuemax=0/valuenow=bytes —
            // the attributes are omitted entirely and the byte count stays in the label.
            Assert.Null(bar.GetAttribute("aria-valuenow"));
            Assert.Null(bar.GetAttribute("aria-valuemax"));
            Assert.Contains("heruntergeladen", cut.Find("#tvsender-download-status").TextContent);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task DownloadSucceeded_ShowsSuccessAlert()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-dl-success", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var downloadService = new FakeFfmpegToolDownloadService(new FfmpegToolDownloadJobSnapshot(
            Guid.NewGuid(), FfmpegToolDownloadJobStatus.Succeeded,
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow,
            "ffmpeg und ffprobe wurden installiert.", null, 1024, 1024));
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("#tvsender-download-success");
            Assert.Contains("alert-success", alert.ClassList);
            Assert.Contains("installiert", alert.TextContent);
            Assert.Contains("Laufzeitermittlung", alert.TextContent);
            // Kein Punkt-Gedankenstrich-Konstrukt: die Snapshot-Message endet bereits mit
            // einem Satzende, der Zusatz ist ein eigener Satz.
            Assert.DoesNotContain(". —", alert.TextContent);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ForeignJobActive_PollingStillPicksUpCompletion()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-dl-foreign", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var downloadService = new FakeFfmpegToolDownloadService();
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: new AbsentFfprobeLocator(),
            downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-download-tools-ffprobe"), TimeSpan.FromSeconds(10));

        // Another admin's job becomes active while this page is already open — the local
        // snapshot was Idle at init, so no poll loop is running yet.
        var foreignJobId = Guid.NewGuid();
        downloadService.SetSnapshot(new FfmpegToolDownloadJobSnapshot(
            foreignJobId, FfmpegToolDownloadJobStatus.Running,
            DateTimeOffset.UtcNow, null, "ffmpeg/ffprobe werden heruntergeladen.", null,
            512, 1024));

        cut.Find("#tvsender-download-tools-ffprobe").Click();
        cut.Find("#tvsender-download-confirm").Click();

        // The service rejected the second start (Started=false) but the returned snapshot
        // is active — the component must still poll, otherwise the card freezes.
        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("#tvsender-download-progress-card")), TimeSpan.FromSeconds(10));

        downloadService.SetSnapshot(new FfmpegToolDownloadJobSnapshot(
            foreignJobId, FfmpegToolDownloadJobStatus.Failed,
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow,
            "Download fehlgeschlagen.", "Der Download ist fehlgeschlagen. Details stehen im Server-Protokoll.",
            512, 1024));

        cut.WaitForAssertion(() => cut.Find("#tvsender-download-error"), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ForeignAcknowledgedCompletion_LoopStillRefreshesToolPaths()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-dl-foreign-ack", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);

        // A foreign job is already running when the page loads, so the poll loop starts.
        var downloadService = new FakeFfmpegToolDownloadService(new FfmpegToolDownloadJobSnapshot(
            Guid.NewGuid(), FfmpegToolDownloadJobStatus.Running,
            DateTimeOffset.UtcNow, null, "ffmpeg/ffprobe werden heruntergeladen.", null,
            512, 1024));
        var ffprobeLocator = new SettableFfprobeLocator();
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: ffprobeLocator,
            downloadService: downloadService);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-ffprobe-missing"), TimeSpan.FromSeconds(10));

        // A second tab observed the terminal state and acknowledged it first — this
        // page's loop only ever reads Idle afterwards. It must still re-resolve the tool
        // paths, otherwise the stale "missing" notice survives until reload although the
        // binaries are installed by now.
        ffprobeLocator.SetPath("tools/ffmpeg/ffprobe");
        downloadService.SetSnapshot(FfmpegToolDownloadJobSnapshot.Idle);

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("#tvsender-ffprobe-missing")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task PassiveObserver_ProbeLoopTick_RefreshesToolPaths()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-passive-tick", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);

        // Passive observer: the page loaded with an Idle snapshot, so no download poll
        // loop runs — only the 15 s probe-refresh loop ticks (driven here by the
        // controllable time provider, like LiveTvTests drives the LiveTv poll).
        var controllableTime = new ControllableTimeProvider();
        var ffprobeLocator = new SettableFfprobeLocator();
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(
            fixture, admin.Id, ffprobeLocator: ffprobeLocator, timeProvider: controllableTime);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-ffprobe-missing"), TimeSpan.FromSeconds(10));

        // A second admin installs the tools — the next probe-refresh tick re-resolves
        // the locators, so the stale "missing" notice disappears without a reload.
        ffprobeLocator.SetPath("tools/ffmpeg/ffprobe");
        controllableTime.TickTimers();

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("#tvsender-ffprobe-missing")), TimeSpan.FromSeconds(10));
    }
}
