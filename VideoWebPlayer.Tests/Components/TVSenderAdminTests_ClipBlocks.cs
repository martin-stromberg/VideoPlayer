using Bunit;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the clip-fill block cards of the "Sendeplan" tab in <see cref="TVSenderAdmin"/>:
/// gapless runs of <c>IsClipFill</c>-marked slots collapse into one
/// <c>.tvsender-clip-block</c> card whose icon button opens the detail overlay listing every
/// clip in air order. Rendered against a real SQLite database like
/// <see cref="TVSenderAdminTests"/>.
/// </summary>
public sealed class TVSenderAdminTests_ClipBlocks
{
    private static async Task<(ProgramRule Rule, DateTime Start)> SeedRuleAsync(
        ProgramScheduleTestDb fixture, CancellationToken ct)
    {
        var channel = await fixture.GetChannelAsync(ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Clipregel",
            ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily, new TimeOnly(20, 0),
            ProgramRuleMaterialType.Movie, materialId: 0);
        // UTC-verankert ab morgen, damit der `Start >= UtcNow-2h`-Filter der Seite
        // die Slots sicher einschliest (Muster Sendeplan_GroupsSlotsByDay_AndMarksRuleChanges).
        return (rule, DateTime.UtcNow.Date.AddDays(1).AddHours(8));
    }

    private static async Task<ProgramSlot> SeedClipSlotAsync(
        ProgramScheduleTestDb fixture, string name, long ruleId,
        DateTime start, int durationMinutes, CancellationToken ct,
        ProgramSlotStatus status = ProgramSlotStatus.Scheduled)
    {
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.SeedProbeItemAsync(name, TimeSpan.FromMinutes(durationMinutes), DateTime.UtcNow, 0, ct);
        return await fixture.CreateSlotAsync(channel.Id, start, start.AddMinutes(durationMinutes), item.Id,
            ruleId: ruleId, status: status, isClipFill: true);
    }

    [Fact]
    public async Task Sendeplan_ClipFillBlock_RendersSingleBlockCard()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-clip-block-card", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (rule, start) = await SeedRuleAsync(fixture, ct);
        var first = await SeedClipSlotAsync(fixture, "clip-eins.mkv", rule.Id, start, 5, ct);
        await SeedClipSlotAsync(fixture, "clip-zwei.mkv", rule.Id, start.AddMinutes(5), 5, ct);
        await SeedClipSlotAsync(fixture, "clip-drei.mkv", rule.Id, start.AddMinutes(10), 5, ct);

        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-sendeplan"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-sendeplan").Click();

        // Ein Block statt drei Einzelkarten: Zeitraum, Gesamtdauer und Clip-Anzahl aggregiert.
        var blocks = cut.FindAll("#tvsender-schedule-cards .program-card.tvsender-clip-block");
        var block = Assert.Single(blocks);
        Assert.Equal(first.Id.ToString(), block.GetAttribute("data-first-slot-id"));
        Assert.Equal("3", block.GetAttribute("data-clip-count"));
        Assert.Contains("Platzhalter-Clips", block.TextContent);
        Assert.Contains("3 Clips", block.TextContent);
        Assert.Contains("Gesamtdauer 00:15:00", block.TextContent);
        Assert.Contains("Regel: Clipregel", block.TextContent);
        Assert.Contains("Geplant", block.TextContent);
        // Zeitraum first.Start–last.End lokal (die Trenner sind &nbsp; im Markup).
        var time = block.QuerySelector(".program-time")!.TextContent;
        Assert.Contains($"{start.ToLocalTime():dd.MM. HH:mm}", time);
        Assert.Contains($"{start.AddMinutes(15).ToLocalTime():HH:mm}", time);
        Assert.Empty(cut.FindAll("#tvsender-schedule-cards .program-card.tvsender-slot-row"));

        // Icon-Button mit zugänglichem Namen.
        var toggle = block.QuerySelector(".tvsender-clip-block-toggle");
        Assert.NotNull(toggle);
        Assert.Equal("Platzhalter-Clips anzeigen", toggle!.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Sendeplan_ClipBlock_DetailButton_OpensDialogWithAllClipsInOrder()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-clip-block-dialog", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (rule, start) = await SeedRuleAsync(fixture, ct);
        var clip1 = await SeedClipSlotAsync(fixture, "clip-eins.mkv", rule.Id, start, 5, ct);
        var clip2 = await SeedClipSlotAsync(fixture, "clip-zwei.mkv", rule.Id, start.AddMinutes(5), 5, ct);
        var clip3 = await SeedClipSlotAsync(fixture, "clip-drei.mkv", rule.Id, start.AddMinutes(10), 5, ct);

        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-sendeplan"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-sendeplan").Click();

        Assert.Empty(cut.FindAll("#tvsender-clip-block-dialog"));
        cut.Find(".tvsender-clip-block-toggle").Click();

        var dialog = cut.Find("#tvsender-clip-block-dialog");
        Assert.Contains("Platzhalter-Clips (3)", dialog.TextContent);
        var rows = dialog.QuerySelectorAll(".tvsender-clip-block-row[data-slot-id]");
        Assert.Equal(3, rows.Count);
        // Reihenfolge = ProgramSlot.Start = Abspielreihenfolge; Titel über den
        // Bare-Item-MediaItem.Name-Fallback der Slot-Cards.
        Assert.Equal(
            new[] { clip1.Id.ToString(), clip2.Id.ToString(), clip3.Id.ToString() },
            rows.Select(r => r.GetAttribute("data-slot-id")).ToArray());
        Assert.Contains("clip-eins.mkv", rows[0].TextContent);
        Assert.Contains("clip-zwei.mkv", rows[1].TextContent);
        Assert.Contains("clip-drei.mkv", rows[2].TextContent);
        Assert.Contains(start.ToLocalTime().ToString("HH:mm"), rows[0].TextContent);

        // Schließen über den Dialog-Button; Overlay-Klick schließt ebenfalls (Muster der
        // übrigen admin-dialog-overlay-Dialoge der Seite).
        dialog.QuerySelectorAll("button").First(b => b.TextContent.Contains("Schließen")).Click();
        Assert.Empty(cut.FindAll("#tvsender-clip-block-dialog"));
        cut.Find(".tvsender-clip-block-toggle").Click();
        var overlay = cut.Find("#tvsender-clip-block-dialog").Closest(".admin-dialog-overlay");
        Assert.NotNull(overlay);
        overlay!.Click();
        Assert.Empty(cut.FindAll("#tvsender-clip-block-dialog"));
    }

    [Fact]
    public async Task Sendeplan_SeparatedClipRuns_RenderSeparateBlocks()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-clip-block-split", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var (rule, start) = await SeedRuleAsync(fixture, ct);

        await SeedClipSlotAsync(fixture, "clip-a1.mkv", rule.Id, start, 5, ct);
        await SeedClipSlotAsync(fixture, "clip-a2.mkv", rule.Id, start.AddMinutes(5), 5, ct);
        // Regulärer Sendeplatz dazwischen — getrennte Läufe bleiben getrennte Blöcke.
        var movie = await fixture.CreateMovieAsync("Dazwischenfilm", duration: TimeSpan.FromMinutes(60));
        var movieItemId = await fixture.Db.MovieMediaItems
            .Where(m => m.MovieId == movie.Id).Select(m => m.MediaItemId).SingleAsync(ct);
        await fixture.CreateSlotAsync(channel.Id, start.AddMinutes(10), start.AddMinutes(70),
            movieItemId, movieId: movie.Id, ruleId: rule.Id);
        await SeedClipSlotAsync(fixture, "clip-b1.mkv", rule.Id, start.AddMinutes(70), 5, ct);
        await SeedClipSlotAsync(fixture, "clip-b2.mkv", rule.Id, start.AddMinutes(75), 5, ct);

        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-sendeplan"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-sendeplan").Click();

        var blocks = cut.FindAll("#tvsender-schedule-cards .program-card.tvsender-clip-block");
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.Equal("2", b.GetAttribute("data-clip-count")));
        Assert.Single(cut.FindAll("#tvsender-schedule-cards .program-card.tvsender-slot-row"));
    }

    [Fact]
    public async Task Sendeplan_DisplacedClip_NotGrouped()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-clip-block-displaced", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (rule, start) = await SeedRuleAsync(fixture, ct);
        await SeedClipSlotAsync(fixture, "clip-eins.mkv", rule.Id, start, 5, ct);
        await SeedClipSlotAsync(fixture, "clip-zwei.mkv", rule.Id, start.AddMinutes(5), 5, ct);
        // Lückenlos anschließender, aber verdrängter Clip-Slot: kein Block-Beitritt —
        // verdrängte Einträge sind reine Traceability-Anzeige.
        await SeedClipSlotAsync(fixture, "clip-verdraengt.mkv", rule.Id, start.AddMinutes(10), 5, ct,
            status: ProgramSlotStatus.Displaced);

        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-sendeplan"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-sendeplan").Click();

        var blocks = cut.FindAll("#tvsender-schedule-cards .program-card.tvsender-clip-block");
        var block = Assert.Single(blocks);
        Assert.Equal("2", block.GetAttribute("data-clip-count"));

        var rows = cut.FindAll("#tvsender-schedule-cards .program-card.tvsender-slot-row");
        var displaced = Assert.Single(rows);
        Assert.Contains("tvsender-slot-displaced", displaced.ClassList);
        Assert.Contains("Verdrängt", displaced.TextContent);
        Assert.Contains("clip-verdraengt.mkv", displaced.TextContent);
    }
}
