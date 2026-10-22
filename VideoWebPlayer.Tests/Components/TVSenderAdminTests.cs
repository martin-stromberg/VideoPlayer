using System.Net;
using System.Security.Claims;
using System.Text;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Client;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the restructured <see cref="TVSenderAdmin"/> page (N13+N14): the four-tab
/// layout, tab switching via Razor state and the expandable pending/failed probe detail
/// tables — rendered against a real SQLite database like <see cref="GenreAdminTests"/>.
/// </summary>
public sealed class TVSenderAdminTests
{
    private const int MaxAttempts = 10;

    private static async Task<(BunitContext Ctx, string AdminId)> CreateContextAsync(
        ProgramScheduleTestDb fixture, string adminId)
    {
        var db = fixture.Db;
        var ruleService = new ProgramRuleService(db);
        var settings = new ProgramSettingsService(db, NullLogger<ProgramSettingsService>.Instance);
        var scheduleService = new ProgramScheduleService(
            db, ruleService, settings, NullLogger<ProgramScheduleService>.Instance);
        var probeService = new MediaDurationProbeService(
            db, new StubMediaSourceReader(), new NullProbe(), new PresentLocator(),
            Options.Create(new MediaProbeOptions()), NullLogger<MediaDurationProbeService>.Instance);

        var ctx = new BunitContext();
        ctx.AddAuthorization()
            .SetAuthorized("admin")
            .SetClaims(
                new Claim("IsAdmin", "True"),
                new Claim(ClaimTypes.NameIdentifier, adminId));
        ctx.Services.AddSingleton<ApplicationDbContext>(db);
        ctx.Services.AddSingleton<IProgramRuleService>(ruleService);
        ctx.Services.AddSingleton<IProgramScheduleService>(scheduleService);
        ctx.Services.AddSingleton<IProgramScheduleSignal>(new ProgramScheduleSignal());
        ctx.Services.AddSingleton(probeService);
        ctx.Services.AddSingleton(settings);
        ctx.Services.AddSingleton(
            new VideoWebPlayerClient(
                new HttpClient(new EmptyListHandler()) { BaseAddress = new Uri("http://localhost/") },
                NullLogger<VideoWebPlayerClient>.Instance));
        ctx.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return (ctx, adminId);
    }

    private static async Task<MediaItem> SeedProbeItemAsync(
        ProgramScheduleTestDb fixture, string name,
        TimeSpan? duration, DateTime? probedAt, int attempts, CancellationToken ct)
    {
        var item = await fixture.CreateMediaItemAsync();
        item.Name = name;
        item.Duration = duration;
        item.DurationProbedAt = probedAt;
        item.DurationProbeAttempts = attempts;
        await fixture.Db.SaveChangesAsync(ct);
        return item;
    }

    [Fact]
    public async Task RendersFourTabs_WithRulesActive()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-tabs", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();

        cut.WaitForAssertion(() =>
        {
            var tabs = cut.FindAll(".admin-tab");
            Assert.Equal(4, tabs.Count);
            Assert.Equal("Regeln", tabs[0].TextContent.Trim());
            Assert.Equal("Sendeplan", tabs[1].TextContent.Trim());
            Assert.Equal("Einstellungen", tabs[2].TextContent.Trim());
            Assert.Equal("Laufzeitermittlung", tabs[3].TextContent.Trim());
        }, TimeSpan.FromSeconds(10));

        // Only the Regeln pane is active; the other panes stay in the DOM (CSS-hidden),
        // so no content reloads on tab switches.
        Assert.Contains("active", cut.Find("#tab-regeln").ClassList);
        Assert.DoesNotContain("active", cut.Find("#tab-sendeplan").ClassList);
        Assert.DoesNotContain("active", cut.Find("#tab-einstellungen").ClassList);
        Assert.DoesNotContain("active", cut.Find("#tab-laufzeit").ClassList);
        Assert.Equal("true", cut.Find("#tvsender-tab-regeln").GetAttribute("aria-selected"));
        Assert.Equal("false", cut.Find("#tvsender-tab-laufzeit").GetAttribute("aria-selected"));

        cut.Find("#tvsender-new-rule");
        cut.Find("#tvsender-name");
        cut.Find("#tvsender-probe-card");
        cut.Find("#tvsender-header-channel");
    }

    [Fact]
    public async Task SwitchesTabs_AndShowsProbeDetailTables()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-probe-details", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;

        await SeedProbeItemAsync(fixture, "fertig.mkv", TimeSpan.FromMinutes(90), now, 0, ct);
        await SeedProbeItemAsync(fixture, "wartet-retry.mkv", null, now.AddMinutes(-5), 1, ct);
        await SeedProbeItemAsync(fixture, "wartet-neu.mkv", null, null, 0, ct);
        await SeedProbeItemAsync(fixture, "defekt.mkv", null, now.AddDays(-1), MaxAttempts, ct);

        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-laufzeit"), TimeSpan.FromSeconds(10));

        cut.Find("#tvsender-tab-laufzeit").Click();

        Assert.Contains("active", cut.Find("#tab-laufzeit").ClassList);
        Assert.DoesNotContain("active", cut.Find("#tab-regeln").ClassList);
        Assert.Equal("true", cut.Find("#tvsender-tab-laufzeit").GetAttribute("aria-selected"));

        Assert.Contains("2 ausstehend", cut.Find("#tvsender-probe-pending").TextContent);
        Assert.Contains("1 fehlgeschlagen", cut.Find("#tvsender-probe-failed").TextContent);

        // Pending detail list: collapsed by default, opened via the chip button.
        var pendingDetail = cut.Find("#tvsender-probe-pending-detail");
        Assert.False(pendingDetail.HasAttribute("open"));
        cut.Find("#tvsender-probe-pending-details").Click();
        Assert.True(cut.Find("#tvsender-probe-pending-detail").HasAttribute("open"));
        var pendingRows = cut.FindAll("#tvsender-probe-pending-detail .tvsender-probe-detail-row");
        Assert.Equal(2, pendingRows.Count);
        Assert.Contains(pendingRows, r => r.TextContent.Contains("wartet-retry.mkv"));
        Assert.Contains(pendingRows, r => r.TextContent.Contains("wartet-neu.mkv"));

        var failedDetail = cut.Find("#tvsender-probe-failed-detail");
        Assert.False(failedDetail.HasAttribute("open"));
        cut.Find("#tvsender-probe-failed-details").Click();
        Assert.True(cut.Find("#tvsender-probe-failed-detail").HasAttribute("open"));
        var failedRows = cut.FindAll("#tvsender-probe-failed-detail .tvsender-probe-detail-row");
        Assert.Single(failedRows);
        Assert.Contains("defekt.mkv", failedRows[0].TextContent);
        Assert.Contains(MaxAttempts.ToString(), failedRows[0].TextContent);
    }

    [Fact]
    public async Task RuleEditor_RendersAsOverlayDialog()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-editor-overlay", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-new-rule"), TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll("#rule-editor"));
        cut.Find("#tvsender-new-rule").Click();

        // The editor is an overlay dialog floating above the rule list, not an embedded card.
        var editor = cut.Find("#rule-editor");
        Assert.Contains("admin-dialog", editor.ClassList);
        var overlay = editor.Closest(".admin-dialog-overlay");
        Assert.NotNull(overlay);
        cut.Find("#rule-name");

        // Clicking the overlay background cancels the edit — the dialog disappears.
        overlay!.Click();
        Assert.Empty(cut.FindAll("#rule-editor"));

        // Reopen and close via the header button instead.
        cut.Find("#tvsender-new-rule").Click();
        cut.FindAll("#rule-editor button").Single(b => b.TextContent.Contains("Schließen")).Click();
        Assert.Empty(cut.FindAll("#rule-editor"));
    }

    [Fact]
    public async Task RuleEditor_TrailerCheckbox_OnlyForFillerRuleType()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-trailer-option", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-new-rule"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-new-rule").Click();

        // Default rule type is not Filler → no trailer option.
        Assert.Empty(cut.FindAll("#rule-material-trailer"));

        cut.Find("#rule-type").Change("Filler");
        cut.WaitForAssertion(() => cut.Find("#rule-material-trailer"), TimeSpan.FromSeconds(5));

        // Repeat/HolidaySpecial must not offer trailer material either (acceptance finding:
        // AllMaterialTypes previously leaked it into both rule types).
        cut.Find("#rule-type").Change("Repeat");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#rule-material-trailer")), TimeSpan.FromSeconds(5));
        cut.Find("#rule-type").Change("HolidaySpecial");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#rule-material-trailer")), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RuleEditor_ClipSourceSelect_OnlyForFillerRuleType_AndSetsMaterial()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-clipsource-option", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var source = await fixture.EnsureSourceAsync(1);
        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-new-rule"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-new-rule").Click();

        // Default rule type is not Filler → no clip-source select.
        Assert.Empty(cut.FindAll("#rule-material-clipsource"));

        cut.Find("#rule-type").Change("Filler");
        cut.WaitForAssertion(() => cut.Find("#rule-material-clipsource"), TimeSpan.FromSeconds(5));

        // The select offers every media source by name.
        var select = cut.Find("#rule-material-clipsource");
        Assert.Contains(source.Name, select.TextContent);

        // Choosing a source fills the material chip.
        select.Change(source.Id.ToString());
        cut.WaitForAssertion(
            () => Assert.Contains($"Clip-Quelle: {source.Name}", cut.Find("#rule-material-selected").TextContent),
            TimeSpan.FromSeconds(5));

        // Non-filler rule types do not offer the clip source.
        cut.Find("#rule-type").Change("Repeat");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#rule-material-clipsource")), TimeSpan.FromSeconds(5));
    }

    /// <summary>Answers every API call with an empty JSON array — the page only reads playlist lists.</summary>
    private sealed class EmptyListHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
    }

    private sealed class NullProbe : IMediaDurationProbe
    {
        public Task<MediaDurationProbeResult> ProbeFileAsync(string filePath, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.Zero));

        public Task<MediaDurationProbeResult> ProbeUriAsync(string uri, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.Zero));

        public Task<MediaDurationProbeResult> ProbeStreamAsync(Stream input, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.Zero));
    }

    private sealed class PresentLocator : IFfprobeLocator
    {
        public string? Resolve() => "ffprobe";
    }

    private sealed class StubMediaSourceReader : IMediaSourceReader
    {
        public IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source) => Enumerable.Empty<MediaEntry>();
        public IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection) => Enumerable.Empty<MediaEntry>();
        public Task<bool> FileExistsAsync(MediaCollection collection, string fileName) => Task.FromResult(false);
        public Task<string?> ReadFileAsync(MediaCollection collection, string fileName) => Task.FromResult<string?>(null);
        public Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName) => Task.FromResult<Stream?>(null);
        public Stream? OpenFileStream(MediaCollection collection, string fileName) => null;
    }
}
