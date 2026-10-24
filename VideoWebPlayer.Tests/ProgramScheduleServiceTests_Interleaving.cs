using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the series interleaving of genre rules (N19): a genre window is filled
/// round-robin across the contained shows — each show gets one episode per day at a stable
/// slot position while its own continuation cursor advances episode by episode.
/// </summary>
public sealed class ProgramScheduleServiceTests_Interleaving
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static DateTime LocalDate(ProgramSlot slot)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(slot.Start, DateTimeKind.Utc), Tz);

    private static async Task<(ProgramScheduleTestDb Fixture, ProgramScheduleService Service, List<ProgramSlot> Slots, List<long> EpA, List<long> EpB, ProgramRule Rule)> SeedGenreWindowAsync(
        string dbName, CancellationToken ct, int episodeCountPerShow = 4)
    {
        var fixture = await ProgramScheduleTestDb.CreateAsync(dbName, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Serienfenster");
        var (showA, _, epA) = await fixture.CreateShowAsync("A-Serie", episodeCountPerShow, duration: TimeSpan.FromMinutes(45));
        var (showB, _, epB) = await fixture.CreateShowAsync("B-Serie", episodeCountPerShow, duration: TimeSpan.FromMinutes(45));
        await fixture.LinkShowGenreAsync(showA.Id, genre.Id);
        await fixture.LinkShowGenreAsync(showB.Id, genre.Id);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Serienfenster", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Genre, genre.Id,
            episodeCount: 2, genreIds: [genre.Id]);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 9, 0)), ct);
        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        return (fixture, service, slots, epA.Select(e => e.Id).ToList(), epB.Select(e => e.Id).ToList(), rule);
    }

    [Fact]
    public async Task EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, _, slots, epA, epB, _) = await SeedGenreWindowAsync("schedule-interleave-stable", ct);
        using var _ = fixture;

        // Window 12:00 daily, 2 strands × 45 min: the same show keeps the same slot position
        // every day — which show leads is decided by the hash mix, not the alphabet.
        var byDay = slots.GroupBy(s => DateOnly.FromDateTime(LocalDate(s))).ToList();
        Assert.True(byDay.Count >= 2, "Mindestens zwei Ausstrahlungstage erwartet.");

        var firstDayOrdered = byDay.OrderBy(g => g.Key).First().OrderBy(s => s.Start).ToList();
        var leadingIsA = epA.Cast<long?>().Contains(firstDayOrdered[0].TVShowEpisodeId);
        var expectedLead = leadingIsA ? epA : epB;
        var expectedSecond = leadingIsA ? epB : epA;

        foreach (var day in byDay)
        {
            var ordered = day.OrderBy(s => s.Start).ToList();
            Assert.Equal(2, ordered.Count);
            Assert.Equal(12, LocalDate(ordered[0]).Hour);
            Assert.Equal(45, LocalDate(ordered[1]).Minute + LocalDate(ordered[1]).Hour * 60 - 12 * 60);
            Assert.Contains(ordered[0].TVShowEpisodeId, expectedLead.Cast<long?>());
            Assert.Contains(ordered[1].TVShowEpisodeId, expectedSecond.Cast<long?>());
        }

        // No double occupancy: slot intervals never overlap.
        var all = slots.OrderBy(s => s.Start).ToList();
        for (var i = 1; i < all.Count; i++)
            Assert.True(all[i].Start >= all[i - 1].End, $"Überlappung: {all[i - 1].End:o} > {all[i].Start:o}");
    }

    [Fact]
    public async Task EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, _, slots, epA, epB, _) = await SeedGenreWindowAsync("schedule-interleave-order", ct);
        using var _ = fixture;

        // Per show the episode numbers advance by one each day (wrap included: 4-episode ring).
        foreach (var (episodes, label) in new[] { (epA, "A"), (epB, "B") })
        {
            var aired = slots.Where(s => s.TVShowEpisodeId is { } id && episodes.Contains(id))
                .OrderBy(s => s.Start).Select(s => episodes.IndexOf(s.TVShowEpisodeId!.Value)).ToList();
            Assert.True(aired.Count >= 2, $"Serie {label}: mindestens zwei Ausstrahlungen erwartet.");
            for (var i = 1; i < aired.Count; i++)
                Assert.Equal((aired[i - 1] + 1) % episodes.Count, aired[i]);
        }

        // Per-strand cursor rows: one row per show.
        var cursors = await fixture.Db.ProgramRuleCursors.ToListAsync(ct);
        Assert.Equal(2, cursors.Count);
        Assert.All(cursors, c => Assert.StartsWith("show:", c.StrandKey));
    }

    [Fact]
    public async Task EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, service, slots, epA, epB, rule) = await SeedGenreWindowAsync("schedule-interleave-cont", ct, episodeCountPerShow: 6);
        using var _ = fixture;

        // Extend coverage a day later — each strand continues at its own persisted cursor,
        // never restarting at the first episode. (Within the 24 h lookback the service
        // re-consumes one already-aired occurrence per strand — the same lookback-consume
        // semantics the flat ring had before; a single index jump across the run boundary
        // is therefore expected, but never a reset.)
        var later = new DateTimeOffset(LocalUtc(2026, 10, 25, 9, 0));
        await service.EnsureCoverageAsync(later, ct);

        var slots2 = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).OrderBy(s => s.Start).ToList();
        Assert.True(slots2.Count > slots.Count);

        var airedA = slots2.Where(s => s.TVShowEpisodeId is { } id && epA.Contains(id))
            .Select(s => epA.IndexOf(s.TVShowEpisodeId!.Value)).ToList();
        var airedB = slots2.Where(s => s.TVShowEpisodeId is { } id && epB.Contains(id))
            .Select(s => epB.IndexOf(s.TVShowEpisodeId!.Value)).ToList();
        var diag = "A:[" + string.Join(",", airedA) + "] B:[" + string.Join(",", airedB) + "] cursors:" +
            string.Join("|", (await fixture.Db.ProgramRuleCursors.ToListAsync(ct)).Select(c => $"{c.StrandKey}={c.LastContentId}"));
        foreach (var aired in new[] { airedA, airedB })
        {
            Assert.True(aired.Count >= 3, diag);
            for (var i = 1; i < aired.Count; i++)
                Assert.True(aired[i] > aired[i - 1], diag); // strictly advancing — no restart
        }
    }

    [Fact]
    public void StrandOrderKey_IsDeterministic_AndNotAlphabetic()
    {
        // Der Hash-Mix muss reproduzierbar sein, aber die alphabetische Reihenfolge brechen:
        // ueber mehrere Regeln hinweg darf nicht immer "show:1" (alphabetisch erste Serie)
        // die kleinste Ordnung haben.
        var keys = new[] { "show:1", "show:2", "show:3", "show:4" };
        var leads = new HashSet<string>();
        var sameOrderTwice = true;
        for (long ruleId = 1; ruleId <= 6; ruleId++)
        {
            var ordered = keys.OrderBy(k => ProgramScheduleService.StrandOrderKey(ruleId, k)).ToList();
            leads.Add(ordered[0]);
            sameOrderTwice &= keys.OrderBy(k => ProgramScheduleService.StrandOrderKey(ruleId, k))
                .SequenceEqual(ordered);
        }
        Assert.True(sameOrderTwice, "Der Ordnungsschluessel ist nicht reproduzierbar.");
        Assert.True(leads.Count > 1, "Ueber alle Regeln fuehrt immer derselbe Strang — die Mischung ist nicht regelabhaengig.");
    }

    [Fact]
    public async Task EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetical()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-interleave-mix", ct);
        using var _ = fixture;
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Mischung");
        var shows = new List<(long ShowId, long[] EpisodeIds)>();
        foreach (var name in new[] { "A-Serie", "B-Serie", "C-Serie" })
        {
            var (show, _, eps) = await fixture.CreateShowAsync(name, 3, duration: TimeSpan.FromMinutes(45));
            await fixture.LinkShowGenreAsync(show.Id, genre.Id);
            shows.Add((show.Id, eps.Select(e => e.Id).ToArray()));
        }

        // Drei Regeln ueber denselben Bestand: die Fuehrungsserie jeder Regel muss exakt der
        // Hash-Permutation folgen (kleinster StrandOrderKey je Regel) — nicht der alphabetischen
        // Reihenfolge und nicht zufaelligem Trimming.
        // Alle Regeln VOR dem einen Coverage-Lauf anlegen: EnsureCoverage ist extend-only und
        // bricht ab, sobald die Abdeckung den Horizont ueberholt — je nach Maschinen-TZ kippt
        // die Occurrence am Horizondrand anders und ein zweiter Lauf materialisierte dann gar
        // nichts mehr (CI-Fehler "Sequence contains no elements").
        var service = fixture.CreateService();
        var rules = new List<ProgramRule>();
        for (var i = 0; i < 3; i++)
        {
            rules.Add(await fixture.CreateRuleAsync(channel.Id, $"Fenster-{i}", ProgramRuleType.GenreRotation,
                ProgramRuleRecurrence.Daily, new TimeOnly(8 + i * 3, 0), ProgramRuleMaterialType.Genre, genre.Id,
                episodeCount: 3, genreIds: [genre.Id]));
        }
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 9, 0)), ct);

        var slots = await fixture.SlotsAsync(ct);
        var leadShowIds = new HashSet<long>();
        foreach (var rule in rules)
        {
            var expectedLead = shows
                .OrderBy(s => ProgramScheduleService.StrandOrderKey(rule.Id, $"show:{s.ShowId}"))
                .First();

            // Anker ist der Slot am exakten UTC-Start der Occurrence vom 25.10.: die Tag-1-
            // Occurrence ist je nach Fensterzeit bereits gestartet und schreibt dann keinen
            // Slot am Fensterbeginn; der 25.10. liegt auf jedem Rechner mitten im Horizont.
            // Der Vergleich auf den UTC-Start statt auf die lokale Slot-Zeit ist zeitzonenfest:
            // Test und Service konvertieren ueber dieselbe Maschinen-TZ (TimeZoneInfo.Local).
            var occurrenceStart = LocalUtc(2026, 10, 25, rule.TimeOfDay.Hour, rule.TimeOfDay.Minute);
            var leadSlot = slots.Single(s => s.ProgramRuleId == rule.Id && s.Start == occurrenceStart);

            // Pick 0 gehoert dem Hash-Min-Strang; die Episode folgt dem Strang-Cursor
            // (die Tag-1-Occurrence hat sie bereits verbraucht) — verglichen wird die Serie.
            var leadShow = shows.Single(s => s.EpisodeIds.Contains(leadSlot.TVShowEpisodeId!.Value));
            Assert.Equal(expectedLead.ShowId, leadShow.ShowId);
            leadShowIds.Add(leadShow.ShowId);
        }

        Assert.True(leadShowIds.Count > 1,
            "Ueber alle Regeln fuehrt immer dieselbe Serie — die Mischung ist nicht regelabhaengig.");
    }

    [Fact]
    public async Task Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, service, _, _, _, _) = await SeedGenreWindowAsync("schedule-interleave-reconcile", ct);
        using var _ = fixture;

        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 30)), ct);

        var dumped = (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced)
            .Select(s => $"{s.Start:dd.HH:mm}-{s.End:HH:mm} ep={s.TVShowEpisodeId}").ToList();
        Assert.True(summary.SlotsDisplaced == 0, "Displaced: " + string.Join(" | ", dumped));
    }
}
