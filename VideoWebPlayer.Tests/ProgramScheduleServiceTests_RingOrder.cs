using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the deterministic ring mix of unordered material rings (issue #297): the
/// expansions keep delivering their domain sort order, but the ring build scatters the
/// order per rule and strand via <see cref="ProgramScheduleService.ItemOrderKey"/> (FNV-1a),
/// so the ring head no longer systematically favors the alphabetically/id-wise/release-date
/// first title. Ordered strands (episodes, playlist entries) and the airings-ordered
/// trailer ring keep their order. All DB tests run against real SQLite via
/// <see cref="ProgramScheduleTestDb"/>.
/// </summary>
public sealed class ProgramScheduleServiceTests_RingOrder
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    // "Now" for the ring-order tests: Saturday noon. Tests with a head/position expectation
    // use Recurrence.Once on the next day (inside the 48 h horizon): a single occurrence —
    // a lookback occurrence of a Daily rule would consume the ring head without producing
    // a slot (BuildOccurrences starts 24 h before the coverage start).
    private static readonly DateTimeOffset Now = new(LocalUtc(2026, 10, 24, 12, 0));
    private static readonly DateOnly OnceDate = new(2026, 10, 25);

    private static async Task<MovieCollection> CreateCollectionAsync(ProgramScheduleTestDb fixture, string name)
    {
        var collection = new MovieCollection { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        return collection;
    }

    private static async Task<(Playlist Playlist, List<PlaylistEntry> Entries)> CreatePlaylistAsync(
        ProgramScheduleTestDb fixture, CancellationToken ct, string name, PlaylistSortMode sortMode, IReadOnlyList<Movie> movies)
    {
        var user = await fixture.AddUserAsync($"{name}@test.local");
        // Der Besitzer braucht Quellenzugriff — sonst ist die Quelle nicht mehr fuer die
        // gesamte (Nicht-Admin-)Zuschauerschaft freigegeben und die Expansion wird leer.
        await fixture.GrantSourceAccessAsync(user.Id, 1);
        var playlist = new Playlist { UserId = user.Id, Name = name, SortMode = sortMode, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        fixture.Db.Playlists.Add(playlist);
        await fixture.SaveDiagAsync();
        var entries = movies
            .Select(m => new PlaylistEntry { PlaylistId = playlist.Id, MediaType = MediaTypeValues.Movie, MediaId = m.Id, AddedAt = DateTime.UtcNow })
            .ToList();
        fixture.Db.PlaylistEntries.AddRange(entries);
        await fixture.SaveDiagAsync();
        return (playlist, entries);
    }

    // Erwartete Ringreihenfolge eines ungeordneten Rings unter der Hash-Mischung.
    private static List<T> HashOrder<T>(IEnumerable<T> items, Func<T, int> key, Func<T, long> tie)
        => items.OrderBy(key).ThenBy(tie).ToList();

    [Fact]
    public void ItemOrderKey_IsDeterministic_AndRuleDependent()
    {
        // Reiner Schluessel-Test (Muster StrandOrderKey_IsDeterministic_AndNotAlphabetic):
        // identische Eingabe → identischer Schluessel; das Kopfelement variiert ueber die
        // Regeln; die Ordnung faellt weder mit der Id- noch der Alphabet-Reihenfolge
        // zusammen.
        var contentIds = new long[] { 11, 22, 33, 44, 55, 66 };
        var leads = new HashSet<long>();
        var sameOrderTwice = true;
        var differsFromIdOrder = false;
        for (long ruleId = 1; ruleId <= 24; ruleId++)
        {
            var ordered = contentIds
                .OrderBy(id => ProgramScheduleService.ItemOrderKey(ruleId, null, ProgramCursorContentType.Movie, id))
                .ThenBy(id => id)
                .ToList();
            leads.Add(ordered[0]);
            sameOrderTwice &= contentIds
                .OrderBy(id => ProgramScheduleService.ItemOrderKey(ruleId, null, ProgramCursorContentType.Movie, id))
                .ThenBy(id => id)
                .SequenceEqual(ordered);
            differsFromIdOrder |= !ordered.SequenceEqual(contentIds);
        }
        Assert.True(sameOrderTwice, "Der Ordnungsschluessel ist nicht reproduzierbar.");
        Assert.True(leads.Count > 1,
            "Ueber alle Regeln fuehrt immer dasselbe Element — die Mischung ist nicht regelabhaengig.");
        Assert.True(differsFromIdOrder,
            "Die Hash-Ordnung faellt fuer jede Regel mit der Id-Reihenfolge zusammen.");
    }

    [Fact]
    public async Task EnsureCoverage_MovieCollectionRing_FirstPick_FollowsItemOrderKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-collection-head", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = await CreateCollectionAsync(fixture, "Abendkino");

        // Film-Ids 1-4: unter ItemOrderKey(Regel 1) ist die Ringordnung [2,3,1,4] — der
        // Kopf ist nicht der Release-/Id-Erste, die Erwartung unterscheidet die Ordnungen.
        var movies = new List<Movie>();
        for (var i = 0; i < 4; i++)
            movies.Add(await fixture.CreateMovieAsync($"Film {i}", collectionId: collection.Id,
                duration: TimeSpan.FromMinutes(90), releaseDate: new DateTime(2000 + i, 1, 1)));

        var rule = await fixture.CreateRuleAsync(channel.Id, "Sonntagsfilm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Once, new TimeOnly(20, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
            episodeCount: 1, singleDate: OnceDate);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        var expectedHead = HashOrder(movies,
            m => ProgramScheduleService.ItemOrderKey(rule.Id, null, ProgramCursorContentType.Movie, m.Id),
            m => m.Id).First();

        // Die Seed-Praemisse: Hash-Kopf ≠ Release-Erster — sonst pruefte der Test nichts.
        Assert.NotEqual(movies.OrderBy(m => m.ReleaseDate).First().Id, expectedHead.Id);
        var slot = Assert.Single(slots);
        Assert.Equal(expectedHead.Id, slot.MovieId);
    }

    [Fact]
    public async Task EnsureCoverage_UnorderedRing_HeadPicks_DistributeAcrossRules()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-distribution", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = await CreateCollectionAsync(fixture, "Rotation");

        const int n = 6;
        const int r = 24;
        var movies = new List<Movie>();
        for (var i = 0; i < n; i++)
            movies.Add(await fixture.CreateMovieAsync($"Rotation {i}", collectionId: collection.Id,
                duration: TimeSpan.FromMinutes(10), releaseDate: new DateTime(2000 + i, 1, 1)));

        // 24 Once-Regeln ueber denselben Materialsatz: verschiedene TimeOfDay, damit sich
        // die Ausstrahlungen nicht ueberlappen; jede Regel zieht ihren eigenen Hash-Kopf.
        var rules = new List<ProgramRule>();
        for (var i = 0; i < r; i++)
        {
            rules.Add(await fixture.CreateRuleAsync(channel.Id, $"Abend {i}", ProgramRuleType.MoviePrimeTime,
                ProgramRuleRecurrence.Once, new TimeOnly(0, 30).Add(TimeSpan.FromMinutes(45 * i)),
                ProgramRuleMaterialType.MovieCollection, collection.Id,
                episodeCount: 1, singleDate: OnceDate));
        }

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var headCounts = new Dictionary<long, int>();
        foreach (var rule in rules)
        {
            var first = slots.Where(s => s.ProgramRuleId == rule.Id).OrderBy(s => s.Start).First();
            var expectedHead = HashOrder(movies,
                m => ProgramScheduleService.ItemOrderKey(rule.Id, null, ProgramCursorContentType.Movie, m.Id),
                m => m.Id).First();
            Assert.Equal(expectedHead.Id, first.MovieId);
            headCounts[expectedHead.Id] = headCounts.GetValueOrDefault(expectedHead.Id) + 1;
        }

        // Akzeptanzkriterium Gleichverteilung — verifiziert an der konkreten Hash-Auspraegung
        // dieses Seeds (Film-Ids 1-6, Regel-Ids 1-24): die Koepfe verteilen sich 5/5/2/3/4/5
        // ueber die sechs Filme; jeder Film ist mindestens einmal Kopf (Erwartung R/N = 4),
        // kein Film haeufiger als ~2·(R/N)+2 ≈ 10 und der Release-/Alphabet-Erste liegt mit
        // 5 ≤ 2·(R/N) = 8 nicht ueberrepraesentiert. Deterministisch — kein Flaky-Risiko.
        var dump = string.Join(", ", headCounts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));
        Assert.All(movies, m => Assert.True(headCounts.GetValueOrDefault(m.Id) >= 1,
            $"Film {m.Id} ist nie Ringkopf (Verteilung: {dump})"));
        Assert.All(headCounts, kv => Assert.True(kv.Value <= 10,
            $"Film {kv.Key} fuehrt {kv.Value} Regeln (Verteilung: {dump})"));
        var releaseFirst = movies.OrderBy(m => m.ReleaseDate).First();
        Assert.True(headCounts.GetValueOrDefault(releaseFirst.Id) <= 2 * (r / n),
            $"Release-Erster {releaseFirst.Id} ueberrepraesentiert: {headCounts.GetValueOrDefault(releaseFirst.Id)}× (Verteilung: {dump})");
    }

    [Fact]
    public async Task EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-genre-other", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Abendmix");

        // Release-Reihenfolge bewusst gegen die Id-Ordnung gedreht: der alte Sortier-Kopf
        // waere Film 3, der ItemOrderKey("other")-Kopf muss fuehren.
        var m1 = await fixture.CreateMovieAsync("Film Eins", duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2002, 1, 1));
        var m2 = await fixture.CreateMovieAsync("Film Zwei", duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2001, 1, 1));
        var m3 = await fixture.CreateMovieAsync("Film Drei", duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2000, 1, 1));
        foreach (var m in new[] { m1, m2, m3 })
            await fixture.LinkMovieGenreAsync(m.Id, genre.Id);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 6, duration: TimeSpan.FromMinutes(45));
        await fixture.LinkShowGenreAsync(show.Id, genre.Id);

        // Mehrstraengige Expansion ("other" + show:{id}): der Film-Strang traegt den
        // Misch-Seed "other" (nicht null — das waere die einstraengige Flachpfad-Nuance).
        var rule = await fixture.CreateRuleAsync(channel.Id, "Genrefenster", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Once, new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id,
            episodeCount: 4, singleDate: OnceDate, genreIds: [genre.Id]);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id)
            .OrderBy(s => s.Start).ToList();
        var movies = new[] { m1, m2, m3 };
        var expectedMovies = HashOrder(movies,
            m => ProgramScheduleService.ItemOrderKey(rule.Id, "other", ProgramCursorContentType.Movie, m.Id),
            m => m.Id).Take(2).Select(m => (long?)m.Id).ToList();

        // Round-robin ueber 2 Straenge mit 4 Picks: je zwei Filme (gemischt) und zwei
        // Episoden (Sendedreihenfolge, unveraendert).
        Assert.Equal(expectedMovies, slots.Where(s => s.MovieId is not null).Select(s => s.MovieId).ToList());
        Assert.Equal(episodes.Take(2).Select(e => (long?)e.Id).ToList(),
            slots.Where(s => s.TVShowEpisodeId is not null).Select(s => s.TVShowEpisodeId).ToList());
    }

    [Fact]
    public async Task EnsureCoverage_PlaylistRing_ManualSortOrder_IsNotMixed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-playlist-manual", ct);
        var channel = await fixture.GetChannelAsync(ct);

        var movies = new List<Movie>();
        for (var i = 0; i < 5; i++)
            movies.Add(await fixture.CreateMovieAsync($"Titel {i}", duration: TimeSpan.FromMinutes(45)));
        var (playlist, entries) = await CreatePlaylistAsync(fixture, ct, "Manuell", PlaylistSortMode.Manual, movies);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Playlist", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Once, new TimeOnly(20, 0), ProgramRuleMaterialType.Playlist, playlist.Id,
            episodeCount: entries.Count, singleDate: OnceDate);

        // SortOrder bewusst gegen die ItemOrderKey-Ordnung gesetzt (Rotation um 1 — bei
        // unterschiedlichen Eintraegen garantiert verschieden). Die ContentId der
        // Playlist-Items ist die PlaylistEntry.Id, nicht die Movie.Id.
        var hashOrder = HashOrder(entries,
            e => ProgramScheduleService.ItemOrderKey(rule.Id, null, ProgramCursorContentType.PlaylistEntry, e.Id),
            e => e.Id);
        for (var i = 0; i < hashOrder.Count; i++)
            hashOrder[i].SortOrder = (i + 1) % hashOrder.Count;
        await fixture.Db.SaveChangesAsync(ct);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var playlistOrder = entries.OrderBy(e => e.SortOrder).Select(e => (long?)e.MediaId).ToList();
        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id)
            .OrderBy(s => s.Start).ToList();
        // Die Slot-Folge entspricht der manuellen SortOrder — nicht dem Hash-Mix:
        // PlaylistEntry-Items sind geordnet (PreservesOrder), IsUnorderedRing greift nicht.
        Assert.NotEqual(hashOrder.Select(e => (long?)e.MediaId).ToList(), playlistOrder);
        Assert.Equal(playlistOrder, slots.Select(s => s.MovieId).ToList());
    }

    [Fact]
    public async Task EnsureCoverage_PlaylistRing_ByReleaseDate_KeepsReleaseOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-playlist-release", ct);
        var channel = await fixture.GetChannelAsync(ct);

        var movies = new List<Movie>();
        for (var i = 0; i < 5; i++)
            movies.Add(await fixture.CreateMovieAsync($"Titel {i}", duration: TimeSpan.FromMinutes(45)));
        var (playlist, entries) = await CreatePlaylistAsync(fixture, ct, "Release", PlaylistSortMode.ByReleaseDate, movies);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Playlist", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Once, new TimeOnly(20, 0), ProgramRuleMaterialType.Playlist, playlist.Id,
            episodeCount: entries.Count, singleDate: OnceDate);

        // Die Release-Ordnung steuert ExpandPlaylistAsync ueber Movie.ReleaseDate — sie wird
        // bewusst gegen die ItemOrderKey-Ordnung der Entry-Ids gedreht (Rotation um 1).
        var hashOrder = HashOrder(entries,
            e => ProgramScheduleService.ItemOrderKey(rule.Id, null, ProgramCursorContentType.PlaylistEntry, e.Id),
            e => e.Id);
        for (var i = 0; i < hashOrder.Count; i++)
            movies.Single(m => m.Id == hashOrder[i].MediaId).ReleaseDate =
                new DateTime(2000 + (i + 1) % hashOrder.Count, 1, 1);
        await fixture.Db.SaveChangesAsync(ct);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var movieById = movies.ToDictionary(m => m.Id);
        var releaseOrder = entries.OrderBy(e => movieById[e.MediaId].ReleaseDate).ThenBy(e => e.AddedAt).ThenBy(e => e.Id)
            .Select(e => (long?)e.MediaId).ToList();
        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id)
            .OrderBy(s => s.Start).ToList();
        Assert.NotEqual(hashOrder.Select(e => (long?)e.MediaId).ToList(), releaseOrder);
        Assert.Equal(releaseOrder, slots.Select(s => s.MovieId).ToList());
    }

    [Fact]
    public async Task EnsureCoverage_ClipSourceRing_FirstPick_FollowsItemOrderKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-clipsource", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var clips = new List<MediaItem>();
        for (var i = 0; i < 4; i++)
            clips.Add(await fixture.CreateMediaItemAsync(1, TimeSpan.FromHours(1)));

        var rule = await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler,
            ProgramRuleRecurrence.Daily, new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1,
            episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        // Der Fueller fuellt den kompletten Horizont; die gezogenen Clips folgen der
        // Ringreihenfolge — die ersten vier Slots sind die gemischte Ordnung, nicht die
        // MediaItemId-Sortierung (Ids 1-4, Hash-Kopf unter Regel 1 ist Id 3).
        var slots = await fixture.SlotsAsync(ct);
        var expectedRing = HashOrder(clips,
            c => ProgramScheduleService.ItemOrderKey(rule.Id, null, ProgramCursorContentType.MediaItem, c.Id),
            c => c.Id);
        Assert.NotEqual(clips[0].Id, expectedRing[0].Id);
        Assert.Equal(expectedRing.Select(c => c.Id).ToList(),
            slots.Take(clips.Count).Select(s => s.MediaItemId).ToList());
    }

    [Fact]
    public async Task EnsureCoverage_ClipGroupRing_FirstPick_FollowsItemOrderKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-clipgroup", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(76));
        var group = await fixture.CreateClipGroupAsync("Zwischenclips", ct);
        var clips = new List<MediaItem>();
        for (var i = 0; i < 12; i++)
        {
            var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
            clips.Add(clip);
            await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
        }

        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Once, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            episodeCount: null, slotDuration: TimeSpan.FromHours(4), singleDate: OnceDate,
            clipGroupIds: [group.Id]);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var clipIds = clips.Select(c => c.Id).ToHashSet();
        var clipSlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id && clipIds.Contains(s.MediaItemId))
            .OrderBy(s => s.Start).ToList();
        var expectedHead = HashOrder(clips,
            c => ProgramScheduleService.ItemOrderKey(rule.Id, $"clipgroup:{group.Id}", ProgramCursorContentType.MediaItem, c.Id),
            c => c.Id).First();

        Assert.NotEmpty(clipSlots);
        // Seed-Praemisse: der Hash-Kopf ist nicht die kleinste MediaItemId (Ids 2-13 → Kopf 13).
        Assert.NotEqual(clips.Min(c => c.Id), expectedHead.Id);
        Assert.Equal(expectedHead.Id, clipSlots[0].MediaItemId);
    }

    [Fact]
    public async Task EnsureCoverage_RingOrder_StableAcrossIndependentFixtures()
    {
        var ct = TestContext.Current.CancellationToken;
        static async Task<List<long?>> SequenceAsync(string dbName, CancellationToken ct)
        {
            using var fixture = await ProgramScheduleTestDb.CreateAsync(dbName, ct);
            var channel = await fixture.GetChannelAsync(ct);
            var collection = await CreateCollectionAsync(fixture, "Sammlung");
            for (var i = 0; i < 5; i++)
                await fixture.CreateMovieAsync($"Film {i}", collectionId: collection.Id,
                    duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2000 + i, 1, 1));
            var rule = await fixture.CreateRuleAsync(channel.Id, "Einmalig", ProgramRuleType.MoviePrimeTime,
                ProgramRuleRecurrence.Once, new TimeOnly(20, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
                episodeCount: 3, singleDate: new DateOnly(2026, 10, 25));
            await fixture.CreateService().EnsureCoverageAsync(
                new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0)), ct);
            return (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id)
                .OrderBy(s => s.Start).Select(s => s.MovieId).ToList();
        }

        // Gleicher Seed in zwei unabhaengigen Fixtures → identische Ids → identische
        // Hash-Ordnung → identische Slot-Folge (Laufinvarianz; Prozessinvarianz sichert der
        // FNV-1a-Unit-Test zusaetzlich ab).
        var sequenceA = await SequenceAsync("ringorder-stable-a", ct);
        var sequenceB = await SequenceAsync("ringorder-stable-b", ct);

        Assert.NotEmpty(sequenceA);
        Assert.Equal(sequenceA, sequenceB);
    }

    [Fact]
    public async Task Reconcile_MixedRing_StaysReproducible()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-reconcile", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = await CreateCollectionAsync(fixture, "Sammlung");
        for (var i = 0; i < 4; i++)
            await fixture.CreateMovieAsync($"Film {i}", collectionId: collection.Id,
                duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2000 + i, 1, 1));

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
            episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(Now, ct);
        var summary = await service.ReconcileAsync(Now.AddMinutes(30), ct);

        // Der Positionsvergleich der Reproduzierbarkeitspruefung re-simuliert denselben
        // SetItems-Aufbau — die Hash-Ordnung ist eine reine Funktion der Eingaben.
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
    }

    [Fact]
    public async Task EnsureCoverage_TrailerRing_KeepsAiringsOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("ringorder-trailer", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var early = await fixture.CreateMovieAsync("Mittagsfilm", duration: TimeSpan.FromMinutes(60));
        var late = await fixture.CreateMovieAsync("Nachtfilm", duration: TimeSpan.FromMinutes(60));
        var earlyTrailer = await CreateMediaItemAndLinkTrailer(fixture, early);
        var lateTrailer = await CreateMediaItemAndLinkTrailer(fixture, late);

        await fixture.CreateRuleAsync(channel.Id, "Mittagsfilm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(13, 0), ProgramRuleMaterialType.Movie, early.Id);
        await fixture.CreateRuleAsync(channel.Id, "Nachtfilm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(23, 0), ProgramRuleMaterialType.Movie, late.Id);
        await fixture.CreateRuleAsync(channel.Id, "Trailer", ProgramRuleType.Filler,
            ProgramRuleRecurrence.Daily, new TimeOnly(0, 0), ProgramRuleMaterialType.Trailer, 0,
            priority: 10, episodeCount: 1);

        // Now 08:00 → erste Luecke bis zur Mittags-Ausstrahlung; beide Trailer haben einen
        // Vorlauf-Treffer im Fenster, die Airings-Reihenfolge (frueheste Ausstrahlung
        // zuerst) fuehrt — nicht der ItemOrderKey, der hier den Nachtfilm-Trailer vorzoeege.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var trailerIds = new[] { earlyTrailer.Id, lateTrailer.Id };
        var trailerSlots = (await fixture.SlotsAsync(ct))
            .Where(s => trailerIds.Contains(s.MediaItemId))
            .OrderBy(s => s.Start).ToList();
        Assert.True(trailerSlots.Count >= 2);
        Assert.Equal(earlyTrailer.Id, trailerSlots[0].MediaItemId);
        Assert.Equal(early.Id, trailerSlots[0].MovieId);
        Assert.Equal(lateTrailer.Id, trailerSlots[1].MediaItemId);
        Assert.Equal(late.Id, trailerSlots[1].MovieId);
    }

    private static async Task<MediaItem> CreateMediaItemAndLinkTrailer(ProgramScheduleTestDb fixture, Movie movie)
    {
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(5));
        fixture.Db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id, IsTrailer = true });
        await fixture.SaveDiagAsync();
        return item;
    }
}
