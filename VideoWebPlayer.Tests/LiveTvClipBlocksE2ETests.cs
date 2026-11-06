using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E for the Live-TV page (Issue #328): a seeded gapless run of
/// <see cref="ProgramSlot.IsClipFill"/> slots collapses in "Die nächsten Stunden" into one
/// aggregated "Platzhalter-Clips" block row — the same grouping the admin "Sendeplan" tab
/// shows — whose icon button expands the individual clips in air order inside the card.
/// </summary>
public sealed class LiveTvClipBlocksE2ETests : TVSenderAdminE2ETestBase
{
    [Fact]
    public async Task LiveTv_ClipRun_RendersAggregatedBlockRow_WithInlineDetail()
    {
        if (SkipBrowser) return;
        var ct = TestContext.Current.CancellationToken;

        await EnableTvSenderAsync();
        var ruleId = await SeedMovieRuleAsync("E2E-Clipregel", 20, 0, 1, slotDuration: TimeSpan.FromHours(4));
        var clipItemIds = await SeedClipItemsAsync(
            ("e2e-livetv-clip-0.mkv", (TimeSpan?)TimeSpan.FromMinutes(5)),
            ("e2e-livetv-clip-1.mkv", (TimeSpan?)TimeSpan.FromMinutes(5)),
            ("e2e-livetv-clip-2.mkv", (TimeSpan?)TimeSpan.FromMinutes(5)));

        // Das Fenster zwischen Seed und Rendern klein halten: der Sendeplan-Worker darf die
        // manuell geschriebenen Slots nicht vor der ersten Anzeige als nicht reproduzierbar
        // verdrängen (vgl. LiveTvE2ETests-Kommentar zu regellosen Slots) — das langsame
        // Login läuft deshalb vor den Slot-Schreibungen.
        await LoginAsync(UserAEmail);

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // N28: Benutzer-Freigabe, sonst bleibt /live-tv hinter der Allowlist verborgen.
            var userA = await db.Users.SingleAsync(u => u.Email == UserAEmail, ct);
            db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = userA.Id });
            var channel = await db.TVChannels.OrderBy(c => c.Id).FirstAsync(ct);
            var start = DateTime.UtcNow.AddMinutes(30);
            foreach (var itemId in clipItemIds)
            {
                db.ProgramSlots.Add(new ProgramSlot
                {
                    TVChannelId = channel.Id,
                    Start = start,
                    End = start.AddMinutes(5),
                    MediaItemId = itemId,
                    ProgramRuleId = ruleId,
                    RuleType = ProgramRuleType.MoviePrimeTime,
                    IsClipFill = true,
                    CreatedAt = DateTime.UtcNow
                });
                start = start.AddMinutes(5);
            }
            await db.SaveChangesAsync(ct);
        }

        await Page.GotoAsync($"{ServerUrl}/live-tv");
        // Erst der Nachweis, dass die Seite fertig geladen hat (Anmeldung, Sender-Freigabe,
        // Guide-Aufruf) — unter Suite-Last dauert das gelegentlich länger als 30 s.
        await Page.WaitForSelectorAsync("#live-tv-no-program, .livetv-upcoming-row",
            new PageWaitForSelectorOptions { Timeout = 60_000 });

        // Eine Block-Karte statt drei Einzelkarten (Sendeplan-Gruppierung).
        var block = Page.Locator(".livetv-upcoming-row.livetv-clip-block");
        await Expect(block).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = 30_000 });
        await Expect(block).ToContainTextAsync("Platzhalter-Clips");
        await Expect(block).ToContainTextAsync("3 Clips");
        await Expect(block).ToContainTextAsync("Gesamtdauer 00:15:00");
        await Expect(Page.Locator(".livetv-upcoming-row", new PageLocatorOptions { HasText = "e2e-livetv-clip-" }))
            .ToHaveCountAsync(0);

        // Icon-Button mit zugänglichem Namen klappt die Einzelclips in der Karte auf —
        // Reihenfolge = Abspielreihenfolge.
        var toggle = block.Locator("button[aria-label='Platzhalter-Clips anzeigen']");
        await Expect(toggle).ToHaveCountAsync(1);
        await toggle.ClickAsync();
        var detailRows = Page.Locator(".livetv-clip-block-detail .livetv-clip-block-row[data-slot-id]");
        await Expect(detailRows).ToHaveCountAsync(3);
        for (var i = 0; i < 3; i++)
            await Expect(detailRows.Nth(i)).ToContainTextAsync($"e2e-livetv-clip-{i}.mkv");
    }
}
