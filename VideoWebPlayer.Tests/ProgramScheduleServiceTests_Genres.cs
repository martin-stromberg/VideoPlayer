using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Genre material expansion: multi-genre union with dedupe, the per-rule media-source filter,
/// hidden/alternate-name resolution (canonical genres), the seasonal visibility window and the
/// legacy <see cref="ProgramRule.MaterialId"/> fallback for rules without join rows.
/// </summary>
public sealed class ProgramScheduleServiceTests_Genres
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<List<ProgramSlot>> GenreSlotsAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.GenreRotation).ToList();

    [Fact]
    public async Task EnsureCoverage_MultiGenreRule_DeduplicatesUnion()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-union", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var gA = await fixture.CreateGenreAsync("Action");
        var gB = await fixture.CreateGenreAsync("Komödie");
        var shared = await fixture.CreateMovieAsync("Beides", duration: TimeSpan.FromMinutes(90));
        var onlyA = await fixture.CreateMovieAsync("Nur A", duration: TimeSpan.FromMinutes(90));
        var onlyB = await fixture.CreateMovieAsync("Nur B", duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieGenreAsync(shared.Id, gA.Id);
        await fixture.LinkMovieGenreAsync(shared.Id, gB.Id);
        await fixture.LinkMovieGenreAsync(onlyA.Id, gA.Id);
        await fixture.LinkMovieGenreAsync(onlyB.Id, gB.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Genremix", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, gA.Id, episodeCount: 3,
            genreIds: [gA.Id, gB.Id]);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await GenreSlotsAsync(fixture, ct);
        // One airing takes 3 items: the union contains each distinct movie exactly once —
        // the movie linked to both genres is not duplicated within the airing.
        var firstAiring = slots.Take(3).Select(s => s.MovieId).ToList();
        Assert.Equal(3, firstAiring.Distinct().Count());
        Assert.Equal(new HashSet<long?>([shared.Id, onlyA.Id, onlyB.Id]), firstAiring.ToHashSet());
    }

    [Fact]
    public async Task EnsureCoverage_GenreSourceFilter_RestrictsSources()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-srcfilter", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var g1 = await fixture.CreateGenreAsync("Familie", sourceId: 1);
        var g2 = await fixture.CreateGenreAsync("Familie", sourceId: 2);
        var m1 = await fixture.CreateMovieAsync("Aus Quelle 1", sourceId: 1, duration: TimeSpan.FromMinutes(90));
        var m2 = await fixture.CreateMovieAsync("Aus Quelle 2", sourceId: 2, duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieGenreAsync(m1.Id, g1.Id);
        await fixture.LinkMovieGenreAsync(m2.Id, g2.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "NurQuelle1", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, g1.Id, episodeCount: 3,
            genreIds: [g1.Id], mediaSourceIds: [1L]);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await GenreSlotsAsync(fixture, ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(m1.Id, s.MovieId));
    }

    [Fact]
    public async Task EnsureCoverage_GenreNoSourceFilter_IncludesAllSources()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-nofilter", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var g1 = await fixture.CreateGenreAsync("Familie", sourceId: 1);
        var g2 = await fixture.CreateGenreAsync("Familie", sourceId: 2);
        var m1 = await fixture.CreateMovieAsync("Aus Quelle 1", sourceId: 1, duration: TimeSpan.FromMinutes(90));
        var m2 = await fixture.CreateMovieAsync("Aus Quelle 2", sourceId: 2, duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieGenreAsync(m1.Id, g1.Id);
        await fixture.LinkMovieGenreAsync(m2.Id, g2.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        // Empty source filter = all sources (legacy default).
        await fixture.CreateRuleAsync(channel.Id, "AlleQuellen", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, g1.Id, episodeCount: 2,
            genreIds: [g1.Id]);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await GenreSlotsAsync(fixture, ct);
        var movieIds = slots.Select(s => s.MovieId).ToList();
        Assert.Contains(m1.Id, movieIds);
        Assert.Contains(m2.Id, movieIds);
    }

    [Fact]
    public async Task EnsureCoverage_HiddenGenreWithAlternate_ItemsRollUpToCanonical()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-altname", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var familie = await fixture.CreateGenreAsync("Familie", sourceId: 1);
        var family = await fixture.CreateGenreAsync("Family", sourceId: 1, isHidden: true, alternateNames: ["Familie"]);
        var legacyItem = await fixture.CreateMovieAsync("Altbestand", duration: TimeSpan.FromMinutes(90));
        var directItem = await fixture.CreateMovieAsync("Direkt", duration: TimeSpan.FromMinutes(90));
        // Legacy link: the item still points at the hidden genre row whose alternate name
        // forwards to the selected canonical genre.
        await fixture.LinkMovieGenreAsync(legacyItem.Id, family.Id);
        await fixture.LinkMovieGenreAsync(directItem.Id, familie.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Familienprogramm", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, familie.Id, episodeCount: 2,
            genreIds: [familie.Id]);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await GenreSlotsAsync(fixture, ct);
        var movieIds = slots.Select(s => s.MovieId).ToList();
        Assert.Contains(legacyItem.Id, movieIds);
        Assert.Contains(directItem.Id, movieIds);
    }

    [Fact]
    public async Task EnsureCoverage_HiddenGenreWithoutAlternate_ProducesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-hidden", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var hidden = await fixture.CreateGenreAsync("Versteckt", isHidden: true);
        var movie = await fixture.CreateMovieAsync("Unsichtbar", duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieGenreAsync(movie.Id, hidden.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Versteckt", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, hidden.Id, episodeCount: 1,
            genreIds: [hidden.Id]);

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        Assert.Empty(await GenreSlotsAsync(fixture, ct));
        Assert.True(summary.OrphanedRules >= 1);
    }

    [Fact]
    public async Task EnsureCoverage_OutOfSeasonGenre_ProducesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-season", ct);
        var channel = await fixture.GetChannelAsync(ct);
        // Seasonal window that is currently inactive (December-only genre, tested in October).
        var seasonal = await fixture.CreateGenreAsync("Weihnachten",
            startDate: new DateTime(2025, 12, 1), endDate: new DateTime(2025, 12, 26));
        var movie = await fixture.CreateMovieAsync("Weihnachtsfilm", duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieGenreAsync(movie.Id, seasonal.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Saison", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, seasonal.Id, episodeCount: 1,
            genreIds: [seasonal.Id]);

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        Assert.Empty(await GenreSlotsAsync(fixture, ct));
        Assert.True(summary.OrphanedRules >= 1);
    }

    [Fact]
    public async Task EnsureCoverage_InSeasonGenre_IsScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-inseason", ct);
        var channel = await fixture.GetChannelAsync(ct);
        // Window that is currently active (spans October 2026 — the test "now").
        var seasonal = await fixture.CreateGenreAsync("Halloween",
            startDate: new DateTime(2025, 10, 1), endDate: new DateTime(2025, 10, 31));
        var movie = await fixture.CreateMovieAsync("Gruselfilm", duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieGenreAsync(movie.Id, seasonal.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Saison", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, seasonal.Id, episodeCount: 1,
            genreIds: [seasonal.Id]);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        Assert.NotEmpty(await GenreSlotsAsync(fixture, ct));
    }

    [Fact]
    public async Task EnsureCoverage_LegacyGenreRuleWithoutJoins_FallsBackToMaterialId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-legacy", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Action");
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieGenreAsync(movie.Id, genre.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        // Old-backup path: a genre rule with MaterialId but no ProgramRuleGenres rows.
        await fixture.CreateRuleAsync(channel.Id, "Altregel", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await GenreSlotsAsync(fixture, ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(movie.Id, s.MovieId));
    }

    [Fact]
    public async Task EnsureCoverage_GenreExpansion_IncludesShowEpisodes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-shows", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Familie");
        var (show, _, episodes) = await fixture.CreateShowAsync("Familienserie", 2, duration: TimeSpan.FromMinutes(45));
        await fixture.LinkShowGenreAsync(show.Id, genre.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Familienabend", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id, episodeCount: 2,
            genreIds: [genre.Id]);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await GenreSlotsAsync(fixture, ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Contains(s.TVShowEpisodeId, episodes.Select(e => (long?)e.Id)));
    }

    // Issue #290: the expansion's time basis is the run's utcNow, not the wall clock — a genre
    // whose one-day seasonal window covers exactly the run's date expands (while the wall clock
    // is out of season), reconciles as a no-op and flips deterministically the day after.
    [Fact]
    public async Task Reconcile_SeasonalGenre_UtcNowBasis_DeterministicExpansion()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-seasonal-utcnow", ct);
        var channel = await fixture.GetChannelAsync(ct);

        // One-day yearly recurring window exactly on the run day — in season at the run's
        // utcNow, out of season at the wall clock (three days later) and the day after.
        var runNow = DateTime.UtcNow.Date.AddDays(-3);
        var seasonal = await fixture.CreateGenreAsync("Saisonal", startDate: runNow, endDate: runNow);
        var movie = await fixture.CreateMovieAsync("Saisonfilm", duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieGenreAsync(movie.Id, seasonal.Id);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Saison", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, seasonal.Id, episodeCount: 1, genreIds: [seasonal.Id]);
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        var now = new DateTimeOffset(runNow);
        await service.EnsureCoverageAsync(now, ct);

        // In season at the run's utcNow — the genre materializes although the wall clock is
        // already three days past the window.
        Assert.NotEmpty(await GenreSlotsAsync(fixture, ct));
        var first = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, first, ct, "In Saison");
        var second = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "In Saison, 2. Lauf");

        // A day later the genre is out of season at the run's basis too — the remaining slots
        // are displaced exactly once (legitimate change) and the plan stays stable afterwards.
        var flip = await service.ReconcileAsync(now.AddDays(1), ct);
        Assert.True(flip.SlotsDisplaced > 0);
        Assert.All((await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced),
            s => Assert.Equal(rule.Id, s.ProgramRuleId));
        var afterFlip = await service.ReconcileAsync(now.AddDays(1), ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, afterFlip, ct, "Nach Saison-Flip");
    }

    [Fact]
    public async Task RuleLinks_CascadeOnRuleAndGenreDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-genre-cascade", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Action");
        var rule = await fixture.CreateRuleAsync(channel.Id, "Mix", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id, episodeCount: 1,
            genreIds: [genre.Id], mediaSourceIds: [1L]);

        Assert.Equal(1, await fixture.Db.ProgramRuleGenres.CountAsync(ct));
        Assert.Equal(1, await fixture.Db.ProgramRuleMediaSources.CountAsync(ct));

        // Rule deletion cascades both link sets.
        fixture.Db.ProgramRules.Remove(rule);
        await fixture.Db.SaveChangesAsync(ct);
        Assert.Equal(0, await fixture.Db.ProgramRuleGenres.CountAsync(ct));
        Assert.Equal(0, await fixture.Db.ProgramRuleMediaSources.CountAsync(ct));

        var rule2 = await fixture.CreateRuleAsync(channel.Id, "Mix2", ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id, episodeCount: 1,
            genreIds: [genre.Id]);
        // Genre deletion cascades the link row.
        fixture.Db.Genres.Remove(genre);
        await fixture.Db.SaveChangesAsync(ct);
        Assert.Equal(0, await fixture.Db.ProgramRuleGenres.CountAsync(g => g.ProgramRuleId == rule2.Id, ct));
    }
}
