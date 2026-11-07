using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the demand-driven start-strand rotation of interleaved airings (follow-up of
/// issue #334): when an airing cannot serve every strand — the typical slot-duration window
/// with many `collection:{id}` strands plus clip grace — the lead strand rotates over
/// consecutive airing days instead of every airing starting again at the first strand of
/// the fixed `StrandOrderKey` order. Windows that serve every strand keep their stable
/// day structure; `EpisodeCount` below the strand count keeps its existing rotation.
/// All DB tests run against real SQLite via <see cref="ProgramScheduleTestDb"/>.
/// </summary>
public sealed class ProgramScheduleServiceTests_StrandRotation
{
    private const int StrandCount = 6;
    private const int HorizonDays = 7;
    private static readonly TimeSpan Slot = TimeSpan.FromHours(4);
    private static readonly TimeSpan MovieDuration = TimeSpan.FromMinutes(115);

    private sealed record RotationScenario(
        ProgramScheduleTestDb Fixture,
        ProgramScheduleService Service,
        ProgramRule Rule,
        List<MovieCollection> Collections,
        // MovieId -> MovieCollectionId and per collection the movie ids in air
        // (release-date) order — the strand's own order the rotation must never touch.
        Dictionary<long, long> CollectionOfMovie,
        Dictionary<long, List<long>> ReleaseOrder);

    /// <summary>Seeds the starvation scenario: a daily 20:15 genre rotation (4 h slot,
    /// clip group activating the placeholder grace) with <paramref name="strandCount"/>
    /// `collection:{id}` strands of <paramref name="moviesPerCollection"/> films à
    /// <paramref name="movieDuration"/> — only two titles fit an airing, so most strands
    /// starve under a fixed start strand. With <paramref name="episodeCount"/> the rule
    /// carries an episode count instead of the slot duration (the old rotation path).</summary>
    /// <param name="dbName">The in-memory SQLite database name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="strandCount">How many movie collections (strands) to seed.</param>
    /// <param name="moviesPerCollection">How many films each collection holds.</param>
    /// <param name="movieDuration">The runtime of every film (default ~2 h).</param>
    /// <param name="episodeCount">When set, the rule carries this episode count instead
    /// of the slot duration — the statically-starved old rotation path.</param>
    /// <returns>The seeded fixture, service, rule and the movie/collection mappings.</returns>
    private static async Task<RotationScenario> SeedCollectionRotationScenarioAsync(
        string dbName, CancellationToken ct, int strandCount = StrandCount,
        int moviesPerCollection = 2, TimeSpan? movieDuration = null, int? episodeCount = null)
    {
        var fixture = await ProgramScheduleTestDb.CreateAsync(dbName, ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Sammlungen");

        var collections = new List<MovieCollection>();
        var collectionOfMovie = new Dictionary<long, long>();
        var releaseOrder = new Dictionary<long, List<long>>();
        for (var c = 0; c < strandCount; c++)
        {
            var collection = new MovieCollection
            {
                Name = $"Reihe {c + 1}",
                MediaSourceId = 1,
                CreatedAt = DateTime.UtcNow
            };
            fixture.Db.MovieCollections.Add(collection);
            await fixture.SaveDiagAsync();
            collections.Add(collection);

            var movies = new List<Movie>();
            for (var i = 0; i < moviesPerCollection; i++)
            {
                var movie = await fixture.CreateMovieAsync($"Reihe {c + 1} Teil {i + 1}",
                    collectionId: collection.Id, duration: movieDuration ?? MovieDuration,
                    releaseDate: new DateTime(2000 + c, 1, 1).AddYears(i));
                await fixture.LinkMovieGenreAsync(movie.Id, genre.Id);
                collectionOfMovie[movie.Id] = collection.Id;
                movies.Add(movie);
            }
            releaseOrder[collection.Id] = movies
                .OrderBy(m => m.ReleaseDate).Select(m => m.Id).ToList();
        }

        // Only the slot-duration rule gets the clip group — the group exists to activate
        // the placeholder grace that caps the title picks below the strand count.
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 4,
            TimeSpan.FromMinutes(1), ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime",
            ProgramRuleType.GenreRotation, ProgramRuleRecurrence.Daily, PrimetimeSeed.Start,
            ProgramRuleMaterialType.Genre, 0,
            episodeCount: episodeCount,
            slotDuration: episodeCount is null ? Slot : null,
            genreIds: [genre.Id],
            clipGroupIds: episodeCount is null ? [clips.Group.Id] : null,
            placeholderGrace: PrimetimeSeed.Grace);
        return new RotationScenario(fixture, fixture.CreateService(), rule,
            collections, collectionOfMovie, releaseOrder);
    }

    private static async Task<List<ProgramSlot>> TitleSlotsAsync(
        RotationScenario scenario, CancellationToken ct)
        => (await scenario.Fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == scenario.Rule.Id
                && s.Status == ProgramSlotStatus.Scheduled && !s.IsClipFill && s.MovieId is not null)
            .OrderBy(s => s.Start)
            .ToList();

    // The collection airing first on each of <paramref name="evenings"/> consecutive
    // evenings, starting at <paramref name="firstEvening"/>.
    private static List<long> LeadCollections(
        RotationScenario scenario, List<ProgramSlot> titleSlots, DateOnly firstEvening, int evenings)
    {
        var leads = new List<long>();
        for (var day = 0; day < evenings; day++)
        {
            var airing = PrimetimeSeed.AiringOf(titleSlots, firstEvening.AddDays(day));
            Assert.NotEmpty(airing);
            leads.Add(scenario.CollectionOfMovie[airing[0].MovieId!.Value]);
        }
        return leads;
    }

    // Repro of the starvation defect: many `collection:{id}` strands + slot duration +
    // clip grace leave only room for two titles per airing — without rotation every
    // evening repeated the same leading strands. Over consecutive evenings the lead
    // collection must change, and over the horizon every collection must get its turn.
    [Fact]
    public async Task EnsureCoverage_ManyCollectionStrands_RotatesLeadStrandAcrossDays()
    {
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SeedCollectionRotationScenarioAsync("strand-rotation-lead", ct);
        using var _ = scenario.Fixture;

        await scenario.Service.EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0)), ct);

        var titleSlots = await TitleSlotsAsync(scenario, ct);
        var leads = LeadCollections(scenario, titleSlots, new DateOnly(2026, 11, 2), 6);

        // The lead strand moves on every day — the old fixed-offset-0 selection served
        // the same two strands of the StrandOrderKey order every single evening.
        for (var i = 1; i < leads.Count; i++)
            Assert.NotEqual(leads[i - 1], leads[i]);

        // Long-term fairness: over the horizon every collection strand airs at all.
        var served = titleSlots.Select(s => scenario.CollectionOfMovie[s.MovieId!.Value])
            .Distinct().OrderBy(id => id).ToList();
        Assert.Equal(scenario.Collections.Select(c => c.Id).OrderBy(id => id).ToList(), served);
    }

    // Ordered collection strands keep their strict release order across the rotation
    // (no part skipped or advanced), and the per-strand cursor rows track the last
    // placed film of each served collection strand.
    [Fact]
    public async Task EnsureCoverage_ManyCollectionStrands_ReleaseOrderAndCursorsPreserved()
    {
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SeedCollectionRotationScenarioAsync("strand-rotation-order", ct);
        using var _ = scenario.Fixture;

        await scenario.Service.EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0)), ct);

        var titleSlots = await TitleSlotsAsync(scenario, ct);

        foreach (var (collectionId, order) in scenario.ReleaseOrder)
        {
            var aired = titleSlots.Where(s => s.MovieId is { } id && order.Contains(id))
                .Select(s => order.IndexOf(s.MovieId!.Value)).ToList();
            for (var i = 1; i < aired.Count; i++)
                Assert.True(aired[i] == (aired[i - 1] + 1) % order.Count,
                    $"Reihenfolgebruch in collection:{collectionId} an Position {i}: " +
                    $"{order[aired[i - 1]]} → {order[aired[i]]}");
        }

        // One persisted cursor row per served collection strand, pointing at the last
        // film that strand actually aired — CommitTaken commits only the winning run.
        var cursors = await scenario.Fixture.Db.ProgramRuleCursors
            .Where(c => c.ProgramRuleId == scenario.Rule.Id
                && c.StrandKey != null && c.StrandKey.StartsWith("collection:"))
            .ToListAsync(ct);
        var servedCollections = titleSlots
            .Select(s => scenario.CollectionOfMovie[s.MovieId!.Value]).Distinct().ToList();
        Assert.Equal(servedCollections.Count, cursors.Count);
        foreach (var collectionId in servedCollections)
        {
            var cursor = cursors.Single(c => c.StrandKey == $"collection:{collectionId}");
            var lastAired = titleSlots
                .Where(s => scenario.CollectionOfMovie[s.MovieId!.Value] == collectionId)
                .Last().MovieId!.Value;
            Assert.Equal(ProgramCursorContentType.Movie, cursor.LastContentType);
            Assert.Equal(lastAired, cursor.LastContentId);
        }
    }

    // Control case: few strands whose short films let every strand contribute to each
    // airing — the served-check finds no starvation, no rotation fires and the strand
    // sequence of every evening stays identical.
    [Fact]
    public async Task EnsureCoverage_AllStrandsServed_NoRotation_StableStructure()
    {
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SeedCollectionRotationScenarioAsync("strand-rotation-stable", ct,
            strandCount: 2, moviesPerCollection: 12, movieDuration: TimeSpan.FromMinutes(45));
        using var _ = scenario.Fixture;

        await scenario.Service.EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0)), ct);

        var titleSlots = await TitleSlotsAsync(scenario, ct);
        var firstEvening = new DateOnly(2026, 11, 2);
        var reference = PrimetimeSeed.AiringOf(titleSlots, firstEvening)
            .Select(s => scenario.CollectionOfMovie[s.MovieId!.Value]).ToList();
        Assert.NotEmpty(reference);
        Assert.Equal(2, reference.Distinct().Count()); // both strands served — the
                                                       // precondition of "no rotation"
        for (var day = 1; day < 5; day++)
        {
            var titles = PrimetimeSeed.AiringOf(titleSlots, firstEvening.AddDays(day))
                .Select(s => scenario.CollectionOfMovie[s.MovieId!.Value]).ToList();
            Assert.Equal(reference, titles);
        }
    }

    // The pick-order-agnostic title-wise validation must not discard the rotated plan:
    // after the rotated coverage fill the reconcile run displaces nothing.
    [Fact]
    public async Task Reconcile_RotatedPlan_StaysScheduledNoFalseDisplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SeedCollectionRotationScenarioAsync("strand-rotation-reconcile", ct);
        using var _ = scenario.Fixture;

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await scenario.Service.EnsureCoverageAsync(now, ct);
        var summary = await scenario.Service.ReconcileAsync(now.AddMinutes(30), ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(scenario.Fixture, summary, ct);
    }

    // Issue #343: the +1/day start offset let adjacent days' served windows overlap —
    // day N served {k, k+1}, day N+1 served {k+1, k+2}, so a single-film collection
    // aired on consecutive days (once as last, once as first pick). While strands starve
    // (more non-empty strands than picks per airing) the served strand sets of
    // consecutive airings must be disjoint: the rotation resumes right behind the last
    // served strand instead of stepping the calendar day.
    [Fact]
    public async Task EnsureCoverage_ManyCollectionStrands_ConsecutiveAiringsServeDisjointStrands()
    {
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SeedCollectionRotationScenarioAsync("strand-rotation-disjoint", ct);
        using var _ = scenario.Fixture;

        await scenario.Service.EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0)), ct);

        var titleSlots = await TitleSlotsAsync(scenario, ct);
        var firstEvening = new DateOnly(2026, 11, 2);
        var previous = PrimetimeSeed.AiringOf(titleSlots, firstEvening)
            .Select(s => scenario.CollectionOfMovie[s.MovieId!.Value]).ToHashSet();
        Assert.NotEmpty(previous);
        Assert.True(previous.Count < StrandCount,
            "Vorbedingung verletzt: der Abend bedient bereits alle Stränge — keine Rotation nötig.");
        for (var day = 1; day < 6; day++)
        {
            var served = PrimetimeSeed.AiringOf(titleSlots, firstEvening.AddDays(day))
                .Select(s => scenario.CollectionOfMovie[s.MovieId!.Value]).ToHashSet();
            Assert.NotEmpty(served);
            var overlap = previous.Intersect(served).ToList();
            Assert.True(overlap.Count == 0,
                $"Bedient-Überlappung {firstEvening.AddDays(day - 1):dd.MM.} → " +
                $"{firstEvening.AddDays(day):dd.MM.}: {string.Join(", ", overlap)}");
            previous = served;
        }
    }

    // The rotation pointer is ground truth, not run state: after the horizon slides on,
    // a second coverage run must resume behind the strand the last persisted airing
    // ended on — the +1/day offset restarted the count and re-served the previous day's
    // strands right at the run boundary.
    [Fact]
    public async Task EnsureCoverage_SecondRun_ContinuesDisjointAcrossRunBoundary()
    {
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SeedCollectionRotationScenarioAsync("strand-rotation-nextrun", ct);
        using var _ = scenario.Fixture;

        await scenario.Service.EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0)), ct);
        await scenario.Service.EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 5, 12, 0)), ct);

        var titleSlots = await TitleSlotsAsync(scenario, ct);
        var firstEvening = new DateOnly(2026, 11, 2);
        var previous = PrimetimeSeed.AiringOf(titleSlots, firstEvening)
            .Select(s => scenario.CollectionOfMovie[s.MovieId!.Value]).ToHashSet();
        Assert.NotEmpty(previous);

        // Horizon-based lower bound: the second run (5.11. 12:00 + 7 days) covers every
        // evening whose occurrence start lies before its horizon end — a coverage break
        // before that must fail loudly instead of slipping under the disjointness check.
        var horizonEnd = PrimetimeSeed.LocalUtc(2026, 11, 5, 12, 0).AddDays(HorizonDays);
        var expectedEvenings = Enumerable.Range(1, 9)
            .Select(d => firstEvening.AddDays(d))
            .Count(e => PrimetimeSeed.LocalUtc(e.Year, e.Month, e.Day,
                PrimetimeSeed.Start.Hour, PrimetimeSeed.Start.Minute) < horizonEnd);

        var checkedDays = 0;
        for (var day = 1; day < 10; day++)
        {
            var served = PrimetimeSeed.AiringOf(titleSlots, firstEvening.AddDays(day))
                .Select(s => scenario.CollectionOfMovie[s.MovieId!.Value]).ToHashSet();
            if (served.Count == 0)
                break; // beyond the horizon of the second run
            checkedDays++;
            var overlap = previous.Intersect(served).ToList();
            Assert.True(overlap.Count == 0,
                $"Bedient-Überlappung {firstEvening.AddDays(day - 1):dd.MM.} → " +
                $"{firstEvening.AddDays(day):dd.MM.}: {string.Join(", ", overlap)}");
            previous = served;
        }
        Assert.True(checkedDays >= expectedEvenings,
            $"Coverage brach nach {checkedDays} geprüften Tagen ab — {expectedEvenings} Abende " +
            "liegen noch im Horizont des zweiten Laufs und müssen eine Ausstrahlung tragen.");
    }

    // Regression of the old path: EpisodeCount below the strand count still rotates the
    // start strand over the days directly — no probe run, no behaviour change.
    [Fact]
    public async Task EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates()
    {
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SeedCollectionRotationScenarioAsync("strand-rotation-epcount", ct,
            strandCount: 3, movieDuration: TimeSpan.FromMinutes(60), episodeCount: 2);
        using var _ = scenario.Fixture;

        await scenario.Service.EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0)), ct);

        var titleSlots = await TitleSlotsAsync(scenario, ct);
        var leads = LeadCollections(scenario, titleSlots, new DateOnly(2026, 11, 2), 4);
        // Alle drei Stränge kommen als Startstrang dran — zwei rotierende Stränge bei
        // einem hungernden dritten würden die alte Grenze >= 2 ebenfalls erfüllen.
        Assert.Equal(3, leads.Distinct().Count());
    }
}
