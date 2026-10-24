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
    // Entspricht dem MediaProbeOptions-Default — über die Options bezogen, damit die Seeds
    // bei einer Default-Änderung weiterhin „fehlgeschlagen" statt „ausstehend" bedeuten.
    private static readonly int MaxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

    private static async Task<(BunitContext Ctx, string AdminId)> CreateContextAsync(
        ProgramScheduleTestDb fixture, string adminId)
    {
        var db = fixture.Db;
        var ruleService = new ProgramRuleService(db);
        var settings = new ProgramSettingsService(db, NullLogger<ProgramSettingsService>.Instance);
        var scheduleService = new ProgramScheduleService(
            db, ruleService, settings, NullLogger<ProgramScheduleService>.Instance);
        var probeService = new MediaDurationProbeService(
            db, new StubMediaSourceReader(), new FixedDurationProbe(TimeSpan.Zero), new PresentLocator(),
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

        await fixture.SeedProbeItemAsync("fertig.mkv", TimeSpan.FromMinutes(90), now, 0, ct);
        await fixture.SeedProbeItemAsync("wartet-retry.mkv", null, now.AddMinutes(-5), 1, ct);
        await fixture.SeedProbeItemAsync("wartet-neu.mkv", null, null, 0, ct);
        await fixture.SeedProbeItemAsync("defekt.mkv", null, now.AddDays(-1), MaxAttempts, ct);

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
        // Versuche-Zelle (td[2]) mit exaktem Vergleich — ein Substring-Match auf die Zeile
        // könnte die Zahl auch aus dem Zeitstempel "Letzter Versuch" beziehen.
        Assert.Equal(MaxAttempts.ToString(), failedRows[0].QuerySelectorAll("td")[2].TextContent.Trim());
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

    [Fact]
    public async Task Sendeplan_RendersProgramCards_WithBadgesPosterAndProgress()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-cards", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);

        var poster = new Picture { Data = [0x42], ContentType = "image/png", Type = "poster" };
        fixture.Db.Pictures.Add(poster);
        await fixture.SaveDiagAsync();
        var movie = await fixture.CreateMovieAsync("Kartenfilm", duration: TimeSpan.FromMinutes(60));
        movie.Plot = "Handlung des Kartenfilms.";
        movie.GenreNames = "Krimi";
        movie.PosterPictureId = poster.Id;
        await fixture.SaveDiagAsync();
        var item = await fixture.Db.MovieMediaItems
            .Where(m => m.MovieId == movie.Id).Select(m => m.MediaItemId).SingleAsync(ct);

        var now = DateTime.UtcNow;
        await fixture.CreateSlotAsync(channel.Id, now.AddMinutes(-30), now.AddMinutes(30), item, movieId: movie.Id);
        await fixture.CreateSlotAsync(channel.Id, now.AddMinutes(30), now.AddMinutes(90), item, movieId: movie.Id,
            status: ProgramSlotStatus.Displaced);

        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-sendeplan"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-sendeplan").Click();

        var cards = cut.FindAll("#tvsender-schedule-cards .program-card.tvsender-slot-row");
        Assert.Equal(2, cards.Count);

        var live = cards[0];
        Assert.Contains("slot-now", live.ClassList);
        Assert.Single(live.QuerySelectorAll(".badge-live"));
        Assert.Single(live.QuerySelectorAll(".progress-track .progress-fill"));
        Assert.Contains("Kartenfilm", live.TextContent);
        Assert.Contains("Handlung des Kartenfilms.", live.TextContent);
        Assert.Contains("Krimi", live.TextContent);
        Assert.Contains("Geplant", live.TextContent);
        var img = live.QuerySelector(".program-poster img");
        Assert.NotNull(img);
        Assert.Contains($"/api/pictures/{poster.Id}", img!.GetAttribute("src"));

        var displaced = cards[1];
        Assert.Contains("tvsender-slot-displaced", displaced.ClassList);
        Assert.Contains("Verdrängt", displaced.TextContent);
        Assert.Empty(displaced.QuerySelectorAll(".badge-live"));
    }

    [Fact]
    public async Task Sendeplan_GroupsSlotsByDay_AndMarksRuleChanges()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-day-groups", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(60));

        var ruleA = await fixture.CreateRuleAsync(channel.Id, "Fruehregel",
            ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily, new TimeOnly(20, 0),
            ProgramRuleMaterialType.MediaSourceClips, materialId: 0, priority: 1);
        var ruleB = await fixture.CreateRuleAsync(channel.Id, "Nachtregel",
            ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily, new TimeOnly(22, 0),
            ProgramRuleMaterialType.Movie, materialId: 0, priority: 2);

        // Zwei Tage; am ersten Tag wechselt die Regel einmal, am zweiten nicht.
        // UTC-verankert ab morgen, damit der `Start >= UtcNow-2h`-Filter der Seite
        // die Slots sicher einschliest; die erwarteten Tage folgen der lokalen
        // Datumsanzeige der Seite (ToLocalTime).
        var day1 = DateTime.UtcNow.Date.AddDays(1);
        var day2 = day1.AddDays(1);
        var slot1Start = day1.AddHours(8);
        var slot4Start = day2.AddHours(8);
        await fixture.CreateSlotAsync(channel.Id, day1.AddHours(8), day1.AddHours(9), item.Id, ruleId: ruleA.Id);
        await fixture.CreateSlotAsync(channel.Id, day1.AddHours(9), day1.AddHours(10), item.Id, ruleId: ruleA.Id);
        await fixture.CreateSlotAsync(channel.Id, day1.AddHours(10), day1.AddHours(11), item.Id, ruleId: ruleB.Id);
        await fixture.CreateSlotAsync(channel.Id, day2.AddHours(8), day2.AddHours(9), item.Id, ruleId: ruleB.Id);

        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-sendeplan"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-sendeplan").Click();

        cut.WaitForAssertion(() =>
        {
            // Je Kalendertag (lokale Zeit) genau ein Header.
            var headers = cut.FindAll("#tvsender-schedule-cards .tvsender-day-header");
            Assert.Equal(2, headers.Count);
            Assert.Equal(slot1Start.ToLocalTime().ToString("yyyy-MM-dd"), headers[0].GetAttribute("data-day"));
            Assert.Equal(slot4Start.ToLocalTime().ToString("yyyy-MM-dd"), headers[1].GetAttribute("data-day"));

            // Genau eine Trennlinie: der Regelwechsel A → B am ersten Tag.
            var dividers = cut.FindAll("#tvsender-schedule-cards .tvsender-rule-divider");
            var divider = Assert.Single(dividers);
            Assert.Equal(ruleB.Id.ToString(), divider.GetAttribute("data-rule-id"));
            Assert.Contains("Nachtregel", divider.TextContent);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ProbeFailedRow_ResetButton_ResetsItemAndRefreshes()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-probe-reset-row", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;

        await fixture.SeedProbeItemAsync("wartet.mkv", null, now.AddMinutes(-5), 1, ct);
        await fixture.SeedProbeItemAsync("defekt.mkv", null, now.AddDays(-1), MaxAttempts, ct);

        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-laufzeit"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-laufzeit").Click();

        // The pending detail table carries no reset action — only failed rows do.
        cut.Find("#tvsender-probe-pending-details").Click();
        Assert.Single(cut.FindAll("#tvsender-probe-pending-detail .tvsender-probe-detail-row"));
        Assert.Empty(cut.FindAll("#tvsender-probe-pending-detail .tvsender-probe-reset"));

        cut.Find("#tvsender-probe-failed-details").Click();
        var resetButton = Assert.Single(
            cut.FindAll("#tvsender-probe-failed-detail .tvsender-probe-reset"));
        resetButton.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("0 fehlgeschlagen", cut.Find("#tvsender-probe-failed").TextContent);
            Assert.Contains("2 ausstehend", cut.Find("#tvsender-probe-pending").TextContent);
        }, TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll("#tvsender-probe-failed-detail"));
        Assert.Contains("zurückgesetzt", cut.Find("#tvsender-probe-action-status").TextContent);
    }

    [Fact]
    public async Task ProbeResetAll_ConfirmDialog_ResetsAllAndShowsStatus()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-probe-reset-all", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;

        await fixture.SeedProbeItemAsync("defekt-1.mkv", null, now.AddDays(-1), MaxAttempts, ct);
        await fixture.SeedProbeItemAsync("defekt-2.mkv", null, now.AddDays(-2), MaxAttempts, ct);

        var (ctx, _) = await CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-laufzeit"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-laufzeit").Click();
        cut.Find("#tvsender-probe-failed-details").Click();

        // Abbrechen schließt den Dialog ohne Änderung.
        cut.Find("#tvsender-probe-reset-all").Click();
        var overlay = cut.Find(".admin-dialog-overlay");
        Assert.Contains("admin-dialog", overlay.QuerySelector(".admin-dialog")!.ClassList);
        overlay.QuerySelectorAll("button").Single(b => b.TextContent.Contains("Abbrechen")).Click();
        Assert.Empty(cut.FindAll(".admin-dialog-overlay"));
        Assert.Equal(2, cut.FindAll("#tvsender-probe-failed-detail .tvsender-probe-detail-row").Count);

        cut.Find("#tvsender-probe-reset-all").Click();
        cut.Find("#probe-reset-all-confirm").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("0 fehlgeschlagen", cut.Find("#tvsender-probe-failed").TextContent);
            Assert.Contains("2 ausstehend", cut.Find("#tvsender-probe-pending").TextContent);
        }, TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll("#tvsender-probe-failed-detail"));
        Assert.Contains("2 Datei(en)", cut.Find("#tvsender-probe-action-status").TextContent);
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

}
