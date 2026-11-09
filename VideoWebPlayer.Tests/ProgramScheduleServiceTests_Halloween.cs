using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Issue „Senderplanung fehlerhaft", scenario group 2: the holiday rule „Halloween" —
/// a genre rotation (deliberately NOT „Feiertags-Spezial") with daily recurrence at
/// 00:00 local, a 1440-minute slot covering the whole day and an annually recurring
/// validity window 10-31 → 10-31, so it fires only on Oct 31st. Its priority 100 takes
/// the day over from the standard-priority primetime rule completely: that day's
/// primetime airing is discarded, never shifted — normal days keep their regular
/// 20:15 airing, an overshooting last Halloween title runs to its end past midnight
/// and the rolling week simulation displaces nothing already planned.
/// </summary>
public sealed class ProgramScheduleServiceTests_Halloween
{
    /// <summary>The holiday the annual window fires on: Oct 31, 2026 — a Saturday,
    /// Europe/Berlin already back on CET (the DST change was Oct 25).</summary>
    /// <value>Oct 31, 2026.</value>
    private static readonly DateOnly HalloweenDay = new(2026, 10, 31);

    /// <summary>UTC instant of local midnight at the start of the given day.</summary>
    /// <param name="day">The local day.</param>
    /// <returns>The UTC instant of local 00:00.</returns>
    private static DateTime DayStartUtc(DateOnly day)
        => PrimetimeSeed.LocalUtc(day.Year, day.Month, day.Day, 0, 0);

    /// <summary>UTC instant of the regular local primetime start (20:15) of a day.</summary>
    /// <param name="day">The local day.</param>
    /// <returns>The UTC instant of local 20:15.</returns>
    private static DateTime PrimetimeStartUtc(DateOnly day)
        => PrimetimeSeed.LocalUtc(day.Year, day.Month, day.Day,
            PrimetimeSeed.Start.Hour, PrimetimeSeed.Start.Minute);

    /// <summary>The local start date of a stored UTC slot time.</summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The local date the slot starts on.</returns>
    private static DateOnly LocalStartDate(ProgramSlot slot)
        => DateOnly.FromDateTime(PrimetimeSeed.ToLocal(slot.Start));

    /// <summary>One seeded scenario: both rules plus the material whitelists the
    /// negative-space assertions need.</summary>
    /// <param name="Primetime">The regular primetime rule (standard priority).</param>
    /// <param name="Halloween">The holiday rule (priority 100, annual window Oct 31).</param>
    /// <param name="SharedMovie">The movie living in the Halloween genre AND a primetime
    /// genre (scenario 2.c).</param>
    /// <param name="HalloweenMovieIds">The whitelist of the seeded Halloween material.</param>
    /// <returns>The seeded scenario references.</returns>
    private sealed record Scenario(
        ProgramRule Primetime,
        ProgramRule Halloween,
        Movie SharedMovie,
        HashSet<long> HalloweenMovieIds);

    /// <summary>Seeds the scenario base of group 2: the regular primetime rule (two movie
    /// genres plus a show strand, own clip group plus the reserved trailer group) and the
    /// Halloween rule — an own genre the primetime does NOT carry, an own clip group plus
    /// the trailer group, priority 100, annual window Oct 31 → Oct 31. The Halloween
    /// material is fifteen movies: fourteen à 100 min plus the shared movie à 97 min.
    /// No prefix of that ring can ever total exactly 1440, so the last scheduled title
    /// always overshoots midnight (scenario 2.d); and because the day needs all fifteen
    /// of them, the shared movie definitely airs on the holiday (scenario 2.c).</summary>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The scenario rules and material whitelists.</returns>
    private static async Task<Scenario> SeedScenarioAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
    {
        var channel = await fixture.GetChannelAsync(ct);

        var (gAction, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Action", 6, TimeSpan.FromMinutes(100));
        var (gComedy, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Komödie", 6, TimeSpan.FromMinutes(95));
        var (gCrime, _, _) = await PrimetimeSeed.AddShowGenreAsync(fixture, "Krimi", "Tatort", 12, TimeSpan.FromMinutes(45));
        var primeClips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Primetime-Clips", 5, TimeSpan.FromMinutes(2), ct);
        var primetime = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [gAction.Id, gComedy.Id, gCrime.Id], [primeClips.Group.Id, ProgramClipGroup.TrailerGroupId]);

        var halloweenGenre = await fixture.CreateGenreAsync("Halloween");
        var halloweenMovieIds = new HashSet<long>();
        for (var i = 1; i <= 14; i++)
        {
            var movie = await fixture.CreateMovieAsync($"Halloween-Film{i}", duration: TimeSpan.FromMinutes(100));
            await fixture.LinkMovieGenreAsync(movie.Id, halloweenGenre.Id);
            halloweenMovieIds.Add(movie.Id);
        }
        // Scenario 2.c: this movie sits in the Halloween genre AND a primetime genre —
        // on the holiday it may only ever appear as a Halloween-rule slot.
        var shared = await fixture.CreateMovieAsync("Halloween-Schnittmenge", duration: TimeSpan.FromMinutes(97));
        await fixture.LinkMovieGenreAsync(shared.Id, halloweenGenre.Id);
        await fixture.LinkMovieGenreAsync(shared.Id, gAction.Id);
        halloweenMovieIds.Add(shared.Id);

        var halloweenClips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Halloween-Clips", 5, TimeSpan.FromMinutes(2), ct);
        var halloween = await PrimetimeSeed.CreateHolidayRuleAsync(fixture, channel.Id, "Halloween",
            new TimeOnly(0, 0), TimeSpan.FromMinutes(1440), priority: 100,
            [halloweenGenre.Id], [halloweenClips.Group.Id, ProgramClipGroup.TrailerGroupId],
            new DateOnly(2000, 10, 31), new DateOnly(2000, 10, 31));

        return new Scenario(primetime, halloween, shared, halloweenMovieIds);
    }

    /// <summary>All slots whose start falls inside the holiday's local 00:00–24:00
    /// window (continuations started before midnight are not part of the window).</summary>
    /// <param name="slots">The slots to filter.</param>
    /// <returns>The slots starting inside the Oct-31 day window.</returns>
    private static List<ProgramSlot> InsideHalloweenDay(IEnumerable<ProgramSlot> slots)
    {
        var start = DayStartUtc(HalloweenDay);
        var end = DayStartUtc(HalloweenDay.AddDays(1));
        return slots.Where(s => s.Start >= start && s.Start < end).ToList();
    }

    /// <summary>Scenario-2.a core assertion: inside the holiday window every scheduled
    /// slot belongs to the Halloween rule — its titles and its clip fill alike — no
    /// primetime-rule slot appears and no non-clip title comes from outside the seeded
    /// Halloween material. The holiday airing itself starts at local 00:00.</summary>
    /// <param name="scheduled">All scheduled slots of the run.</param>
    /// <param name="scenario">The seeded scenario.</param>
    private static void AssertHalloweenDayPurity(IReadOnlyCollection<ProgramSlot> scheduled, Scenario scenario)
    {
        var inside = InsideHalloweenDay(scheduled).OrderBy(s => s.Start).ToList();
        var dump = string.Join(" | ", inside.Select(s =>
            $"{PrimetimeSeed.ToLocal(s.Start):dd.MM. HH:mm}-{PrimetimeSeed.ToLocal(s.End):dd.MM. HH:mm} rule={s.ProgramRuleId} movie={s.MovieId} ep={s.TVShowEpisodeId} clip={s.IsClipFill}"));
        Assert.NotEmpty(inside);
        // Regelbezug: nothing but the Halloween rule may occupy the holiday window.
        Assert.All(inside, s => Assert.True(s.ProgramRuleId == scenario.Halloween.Id,
            $"Slot ohne Halloween-Regel im 31.10.-Fenster: {dump}"));
        // Material-Whitelist: every non-clip title comes from the Halloween material.
        Assert.All(inside.Where(s => !s.IsClipFill), s =>
        {
            Assert.True(s.MovieId is not null && scenario.HalloweenMovieIds.Contains(s.MovieId.Value),
                $"Nicht-Clip-Titel außerhalb der Halloween-Materialmenge: {dump}");
            Assert.Null(s.TVShowEpisodeId);
        });
        Assert.Equal(DayStartUtc(HalloweenDay), inside.Min(s => s.Start));
        // Nothing foreign may reach across the window edge either — the previous day's
        // primetime tail (titles or clips) must end at or before local midnight.
        Assert.DoesNotContain(scheduled, s => s.Start < DayStartUtc(HalloweenDay)
            && s.End > DayStartUtc(HalloweenDay));
    }

    // Scenario 2.a — Ganztag: the first run happens before Oct 31 (Oct 30 noon local,
    // 7-day horizon). The Oct-31 day window holds exclusively Halloween-rule slots.
    [Fact]
    public async Task EnsureCoverage_HalloweenDay_OnlyHalloweenRuleSlotsInsideWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("halloween-wholeday", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var scenario = await SeedScenarioAsync(fixture, ct);

        await fixture.CreateService().EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 10, 30, 12, 0)), ct);

        var scheduled = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();
        AssertHalloweenDayPurity(scheduled, scenario);

        // The whole holiday is covered — the airing reaches the end of the day window.
        var inside = InsideHalloweenDay(scheduled);
        Assert.True(inside.Max(s => s.End) >= DayStartUtc(HalloweenDay.AddDays(1)),
            "Die Halloween-Ausstrahlung deckt den 31.10. nicht bis 24:00 ab.");
    }

    // Scenario 2.b — Normaltag: Oct 30 and Nov 1 keep a regular primetime airing (real
    // titles starting 20:15 local under the primetime rule); the Halloween rule owns no
    // slot outside Oct 31 — no titles, no clips on the normal days.
    [Fact]
    public async Task EnsureCoverage_NormalDays_PrimetimeRegularNoHalloweenSlots()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("halloween-normaldays", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var scenario = await SeedScenarioAsync(fixture, ct);

        await fixture.CreateService().EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 10, 30, 12, 0)), ct);

        var scheduled = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();
        var primetimeSlots = scheduled.Where(s => s.ProgramRuleId == scenario.Primetime.Id).ToList();
        foreach (var day in new[] { new DateOnly(2026, 10, 30), HalloweenDay.AddDays(1) })
        {
            var airing = PrimetimeSeed.AiringOf(primetimeSlots, day);
            Assert.NotEmpty(airing);
            Assert.Contains(airing, s => !s.IsClipFill);
            Assert.Equal(PrimetimeStartUtc(day), airing[0].Start);
        }

        // Negativraum: every Halloween-rule slot — titles and clips — starts on the
        // holiday itself; nothing of the rule exists on a normal day.
        var halloweenSlots = scheduled.Where(s => s.ProgramRuleId == scenario.Halloween.Id).ToList();
        Assert.NotEmpty(halloweenSlots);
        Assert.All(halloweenSlots, s => Assert.Equal(HalloweenDay, LocalStartDate(s)));
    }

    // Scenario 2.c — Schnittmenge: the movie shared between the Halloween genre and a
    // primetime genre appears on the holiday — exclusively as a Halloween-rule slot.
    // On normal days it may rotate through the primetime (allowed, not asserted).
    [Fact]
    public async Task EnsureCoverage_SharedMovie_OnHolidayOnlyAsHalloweenSlot()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("halloween-shared", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var scenario = await SeedScenarioAsync(fixture, ct);

        await fixture.CreateService().EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 10, 30, 12, 0)), ct);

        var scheduled = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

        // The day needs all fifteen Halloween titles — the shared movie is among them.
        var inside = InsideHalloweenDay(scheduled);
        Assert.Contains(inside, s => s.MovieId == scenario.SharedMovie.Id
            && s.ProgramRuleId == scenario.Halloween.Id && !s.IsClipFill);
        // Negativraum: on the holiday it never airs as a primetime-rule slot.
        Assert.DoesNotContain(scheduled, s => s.MovieId == scenario.SharedMovie.Id
            && s.ProgramRuleId == scenario.Primetime.Id && LocalStartDate(s) == HalloweenDay);
    }

    // Scenario 2.d — Overshoot: the last Halloween title started inside the day runs
    // past local midnight into Nov 1 and completes. No Halloween-rule slot starts on
    // or after Nov 1 — the expired period leaves nothing behind — and the Nov 1
    // primetime begins regularly at 20:15.
    [Fact]
    public async Task EnsureCoverage_HalloweenOvershoot_TitleCompletesThenNothingUntilPrimetime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("halloween-overshoot", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var scenario = await SeedScenarioAsync(fixture, ct);

        await fixture.CreateService().EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 10, 30, 12, 0)), ct);

        var scheduled = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();
        var halloweenSlots = scheduled.Where(s => s.ProgramRuleId == scenario.Halloween.Id)
            .OrderBy(s => s.Start).ToList();
        var titles = halloweenSlots.Where(s => !s.IsClipFill).ToList();
        Assert.NotEmpty(titles);

        // The last title started on Oct 31 and airs past local midnight — complete,
        // never cut at the period end (a full movie runtime, not a fragment).
        var last = titles[^1];
        Assert.Equal(HalloweenDay, LocalStartDate(last));
        var nextDayStart = DayStartUtc(HalloweenDay.AddDays(1));
        Assert.True(last.End > nextDayStart,
            $"Der letzte Halloween-Titel endet {PrimetimeSeed.ToLocal(last.End):dd.MM. HH:mm} — kein Overshoot über Mitternacht.");
        Assert.True(last.End - last.Start >= TimeSpan.FromMinutes(90),
            "Der Overshoot-Titel wurde beschnitten statt zu Ende zu laufen.");

        // Negativraum: nothing of the Halloween rule starts on/after Nov 1 local
        // midnight — the expired period contributes no titles and no clips.
        Assert.All(halloweenSlots, s => Assert.Equal(HalloweenDay, LocalStartDate(s)));

        // The Nov 1 primetime starts regularly at 20:15 local.
        Assert.Contains(scheduled, s => s.ProgramRuleId == scenario.Primetime.Id
            && s.Start == PrimetimeStartUtc(HalloweenDay.AddDays(1)));
    }

    // Scenario 2.e — Verworfen statt verschoben (Soll): the Oct-31 primetime
    // occurrence is discarded — its window is fully taken. No primetime-rule slot may
    // exist between the holiday's start and the next regular airing (Nov 1, 20:15):
    // neither the normal airing nor a shifted „nachgeschobene" copy of it.
    [Fact]
    public async Task EnsureCoverage_HalloweenDay_PrimetimeOccurrenceDiscardedNotShifted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("halloween-discarded", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var scenario = await SeedScenarioAsync(fixture, ct);

        await fixture.CreateService().EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 10, 30, 12, 0)), ct);

        var scheduled = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();
        var forbidden = scheduled
            .Where(s => s.ProgramRuleId == scenario.Primetime.Id
                && s.Start >= DayStartUtc(HalloweenDay)
                && s.Start < PrimetimeStartUtc(HalloweenDay.AddDays(1)))
            .OrderBy(s => s.Start).ToList();
        var dump = string.Join(" | ", forbidden.Select(s =>
            $"{PrimetimeSeed.ToLocal(s.Start):dd.MM. HH:mm}-{PrimetimeSeed.ToLocal(s.End):dd.MM. HH:mm} movie={s.MovieId} ep={s.TVShowEpisodeId} clip={s.IsClipFill}"));
        Assert.True(forbidden.Count == 0,
            $"Primetime-Slots der verworfenen 31.10.-Ausstrahlung existieren (verschoben statt verworfen): {dump}");

        // The next regular airing — the Nov-1 occurrence at 20:15 — is of course there.
        Assert.Contains(scheduled, s => s.ProgramRuleId == scenario.Primetime.Id
            && s.Start == PrimetimeStartUtc(HalloweenDay.AddDays(1)));
    }

    // Scenario 2.f — Wochensimulation: daily worker steps (reconcile + coverage in the
    // worker's own order) from Oct 27 through Nov 1. Normal evenings keep their
    // primetime, Oct 31 is the holiday, and no run displaces anything already planned.
    [Fact]
    public async Task Simulation_WeekOverHalloween_NormalDaysStableNoDisplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("halloween-week", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var scenario = await SeedScenarioAsync(fixture, ct);

        var start = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 10, 27, 12, 0));
        var previous = new List<ProgramSlot>();
        for (var day = 0; day < 6; day++)
        {
            var now = start.AddDays(day);
            var (reconcile, _) = await PrimetimeSeed.StepAsync(fixture, now, ct);
            await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, reconcile, ct, $"Lauf {now:dd.MM.}");
            var current = await fixture.SlotsAsync(ct);
            PrimetimeSeed.AssertPreserved(previous, current, $"Lauf {now:dd.MM.}");
            previous = current;
        }

        var scheduled = previous.Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

        // Every normal evening keeps a primetime airing at its regular 20:15 start —
        // only the holiday drops out of the primetime sequence.
        foreach (var day in new[]
        {
            new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29),
            new DateOnly(2026, 10, 30), HalloweenDay.AddDays(1)
        })
        {
            var airing = PrimetimeSeed.AiringOf(
                scheduled.Where(s => s.ProgramRuleId == scenario.Primetime.Id), day);
            Assert.NotEmpty(airing);
            Assert.Contains(airing, s => !s.IsClipFill);
            Assert.Equal(PrimetimeStartUtc(day), airing[0].Start);
        }

        AssertHalloweenDayPurity(scheduled, scenario);

        // Negativraum: the Halloween rule owns no slot outside the holiday.
        Assert.All(scheduled.Where(s => s.ProgramRuleId == scenario.Halloween.Id),
            s => Assert.Equal(HalloweenDay, LocalStartDate(s)));
    }
}
