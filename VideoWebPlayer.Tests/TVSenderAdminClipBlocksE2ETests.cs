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
/// Playwright E2E tests for the clip-fill block cards of the "Sendeplan" tab: a seeded run of
/// <c>IsClipFill</c>-marked slots renders as one aggregated block card whose icon button opens
/// the detail overlay listing every clip in air order (Muster
/// <see cref="TVSenderAdminClipGroupsE2ETests"/>: Slots direkt per
/// <c>Services.CreateScope()</c> + <see cref="ApplicationDbContext"/> geseedet).
/// </summary>
public sealed class TVSenderAdminClipBlocksE2ETests : TVSenderAdminE2ETestBase
{
    private Task OpenAdminPageAsAdminAsync()
        => OpenAdminPageAsAdminAsync("#tvsender-tab-sendeplan");

    private Task OpenSendeplanTabAsync() => SwitchTabAsync("#tvsender-tab-sendeplan", "#tab-sendeplan");

    /// <summary>Seeds a program rule plus clip-fill slots (<see cref="ProgramSlot.IsClipFill"/>)
    /// directly in the database: <paramref name="clipCounts"/> describes consecutive gapless
    /// runs, each separated from the previous one by a regular (non-clip-fill) slot.
    /// Returns the rule id and the clip media-item ids per run.</summary>
    private async Task<(long RuleId, List<long[]> ClipItemIds)> SeedClipRunsAsync(params int[] clipCounts)
    {
        var ct = TestContext.Current.CancellationToken;
        var clipItemIds = new List<long[]>();
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var channel = await new ProgramRuleService(db).GetOrCreateChannelAsync(ct);
            var rule = new ProgramRule
            {
                TVChannelId = channel.Id,
                Name = "E2E-Clipregel",
                RuleType = ProgramRuleType.MoviePrimeTime,
                Recurrence = ProgramRuleRecurrence.Daily,
                TimeOfDay = new TimeOnly(20, 0),
                SlotDuration = TimeSpan.FromHours(4),
                MaterialType = ProgramRuleMaterialType.Movie,
                MaterialId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ProgramRules.Add(rule);
            await db.SaveChangesAsync(ct);

            var clipNames = clipCounts
                .SelectMany((count, run) => Enumerable.Range(0, count)
                    .Select(i => ($"e2e-block-{run}-{i}.mkv", (TimeSpan?)TimeSpan.FromMinutes(5))))
                .ToList();
            var separatorCount = clipCounts.Length - 1;
            for (var s = 0; s < separatorCount; s++)
                clipNames.Add(($"e2e-trenner-{s}.mkv", (TimeSpan?)TimeSpan.FromMinutes(60)));
            var allItems = await SeedClipItemsAsync(clipNames.ToArray());
            var separators = allItems.Skip(allItems.Count - separatorCount).ToList();

            var index = 0;
            var start = DateTime.UtcNow.Date.AddDays(1).AddHours(8);
            foreach (var (count, run) in clipCounts.Select((c, i) => (c, i)))
            {
                var runIds = new long[count];
                for (var i = 0; i < count; i++)
                {
                    var itemId = allItems[index++];
                    runIds[i] = itemId;
                    db.ProgramSlots.Add(new ProgramSlot
                    {
                        TVChannelId = channel.Id,
                        Start = start,
                        End = start.AddMinutes(5),
                        MediaItemId = itemId,
                        ProgramRuleId = rule.Id,
                        RuleType = rule.RuleType,
                        IsClipFill = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    start = start.AddMinutes(5);
                }
                clipItemIds.Add(runIds);

                if (run < separatorCount)
                {
                    // Regulärer Sendeplatz als Trenner zwischen den Läufen (kein IsClipFill).
                    db.ProgramSlots.Add(new ProgramSlot
                    {
                        TVChannelId = channel.Id,
                        Start = start,
                        End = start.AddMinutes(60),
                        MediaItemId = separators[run],
                        ProgramRuleId = rule.Id,
                        RuleType = rule.RuleType,
                        CreatedAt = DateTime.UtcNow
                    });
                    start = start.AddMinutes(60);
                }
            }
            await db.SaveChangesAsync(ct);
            return (rule.Id, clipItemIds);
        }
    }

    [Fact]
    public async Task Sendeplan_ClipFillBlock_ShowsBlockCardAndDetailOverlay()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var (_, clipItemIds) = await SeedClipRunsAsync(5, 2);

        await OpenAdminPageAsAdminAsync();
        await OpenSendeplanTabAsync();

        // Zwei getrennte Läufe → zwei Block-Karten; der lange Lauf (5 Clips) ist der erste.
        var blocks = Page.Locator(".tvsender-clip-block");
        await Expect(blocks).ToHaveCountAsync(2, new LocatorAssertionsToHaveCountOptions { Timeout = 30_000 });
        var block = blocks.First;
        await Expect(block.Locator(".program-title")).ToHaveTextAsync("Platzhalter-Clips");
        await Expect(block).ToContainTextAsync("5 Clips");
        await Expect(block).ToContainTextAsync("Gesamtdauer 00:25:00");
        await Expect(block).ToContainTextAsync("Regel: E2E-Clipregel");

        // Die Einzelclips der Blöcke erscheinen nicht als eigene Slot-Karten.
        await Expect(Page.Locator(".tvsender-slot-row", new PageLocatorOptions { HasText = "e2e-block-" }))
            .ToHaveCountAsync(0);

        // Icon-Button mit zugänglichem Namen öffnet die Detailansicht.
        var toggle = block.Locator("button[aria-label='Platzhalter-Clips anzeigen']");
        await Expect(toggle).ToHaveCountAsync(1);
        await toggle.ClickAsync();

        var dialog = Page.Locator("#tvsender-clip-block-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("Platzhalter-Clips (5)");
        var rows = dialog.Locator(".tvsender-clip-block-row[data-slot-id]");
        await Expect(rows).ToHaveCountAsync(5);
        // Reihenfolge = ProgramSlot.Start = Abspielreihenfolge.
        for (var i = 0; i < clipItemIds[0].Length; i++)
            await Expect(rows.Nth(i)).ToContainTextAsync($"e2e-block-0-{i}.mkv");

        // Schließen-Button — die Block-Karte bleibt unverändert.
        await dialog.Locator("button", new LocatorLocatorOptions { HasText = "Schließen" }).First.ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(blocks).ToHaveCountAsync(2);

        // Erneut öffnen und per Overlay-Klick schließen.
        await toggle.ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator(".admin-dialog-overlay", new PageLocatorOptions { Has = dialog.First }).ClickAsync(
            new LocatorClickOptions { Position = new Position { X = 5, Y = 5 } });
        await Expect(Page.Locator("#tvsender-clip-block-dialog")).ToHaveCountAsync(0);
        await Expect(blocks).ToHaveCountAsync(2);
    }
}
