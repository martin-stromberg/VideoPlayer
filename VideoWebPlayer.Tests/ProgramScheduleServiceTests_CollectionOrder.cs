using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for ordered movie-collection strands (issue #334): films of the same collection
/// air in release-date order — sequels never before their predecessors. Collection films
/// form their own ordered strand (key "collection:{id}") with a persisted cursor, inside
/// genre rotations they interleave with the other strands; the unordered hash mix only
/// applies to collection-free movies. All DB tests run against real SQLite via
/// <see cref="ProgramScheduleTestDb"/>.
/// </summary>
public sealed class ProgramScheduleServiceTests_CollectionOrder
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    private static readonly DateTimeOffset Now = new(LocalUtc(2026, 10, 24, 12, 0)); // Saturday noon

    private static async Task<MovieCollection> CreateCollectionAsync(ProgramScheduleTestDb fixture, string name)
    {
        var collection = new MovieCollection { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        return collection;
    }

    [Fact]
    public async Task EnsureCoverage_MovieCollection_AirsInReleaseOrder_AndContinuesAcrossRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("collection-order-sequence", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = await CreateCollectionAsync(fixture, "Reihe");

        // Anlage-Reihenfolge bewusst gegen das Releasedatum gedreht — nur das
        // Releasedatum darf die Sendereihenfolge bestimmen.
        var m3 = await fixture.CreateMovieAsync("Teil 3", collectionId: collection.Id, duration: TimeSpan.FromMinutes(90), releaseDate: new DateTime(2002, 1, 1));
        var m1 = await fixture.CreateMovieAsync("Teil 1", collectionId: collection.Id, duration: TimeSpan.FromMinutes(90), releaseDate: new DateTime(2000, 1, 1));
        var m5 = await fixture.CreateMovieAsync("Teil 5", collectionId: collection.Id, duration: TimeSpan.FromMinutes(90), releaseDate: new DateTime(2004, 1, 1));
        var m2 = await fixture.CreateMovieAsync("Teil 2", collectionId: collection.Id, duration: TimeSpan.FromMinutes(90), releaseDate: new DateTime(2001, 1, 1));
        var m4 = await fixture.CreateMovieAsync("Teil 4", collectionId: collection.Id, duration: TimeSpan.FromMinutes(90), releaseDate: new DateTime(2003, 1, 1));
        var releaseOrder = new[] { m1.Id, m2.Id, m3.Id, m4.Id, m5.Id };

        var rule = await fixture.CreateRuleAsync(channel.Id, "Abendfilm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
            episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(Now, ct);

        // Samstag + Sonntag je 2 Filme: die ersten vier Sendeplaetze folgen der
        // Releasedatum-Reihenfolge — nicht der Anlage- oder Hash-Reihenfolge.
        var firstRun = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id)
            .Select(s => s.MovieId).ToList();
        Assert.Equal(releaseOrder.Take(4).Select(id => (long?)id).ToList(), firstRun);

        // Der Fortsetzungs-Cursor ueberlebt den Lauf: der nächste Planlauf setzt mit
        // Teil 5 fort und laeuft danach wieder von vorn (Ringumlauf in Reihenfolge).
        await service.EnsureCoverageAsync(Now.AddDays(2), ct);
        var mondaySlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id
                && s.Start >= LocalUtc(2026, 10, 26, 0, 0) && s.Start < LocalUtc(2026, 10, 27, 0, 0))
            .Select(s => s.MovieId).ToList();
        Assert.Equal(new long?[] { m5.Id, m1.Id }, mondaySlots);
    }

    [Fact]
    public async Task EnsureCoverage_GenreRule_CollectionStrand_KeepsReleaseOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("collection-order-genre", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Abendmix");
        var collection = await CreateCollectionAsync(fixture, "Reihe");

        // Drei Stränge: collection:{id} (geordnet, Releasedatum), "other" (sammlungslose
        // Filme, Hash-Mix), show:{id} (Episoden-Sendereihenfolge).
        var c2 = await fixture.CreateMovieAsync("Reihe 2", collectionId: collection.Id, duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2001, 1, 1));
        var c1 = await fixture.CreateMovieAsync("Reihe 1", collectionId: collection.Id, duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2000, 1, 1));
        var c3 = await fixture.CreateMovieAsync("Reihe 3", collectionId: collection.Id, duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2002, 1, 1));
        var standalone = await fixture.CreateMovieAsync("Einzelner", duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(1999, 1, 1));
        foreach (var m in new[] { c1, c2, c3, standalone })
            await fixture.LinkMovieGenreAsync(m.Id, genre.Id);
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 6, duration: TimeSpan.FromMinutes(45));
        await fixture.LinkShowGenreAsync(show.Id, genre.Id);

        // 3 Stränge, 3 Picks je Ausstrahlung: jeder Strang traegt genau einen Titel bei —
        // die Sammlung bringt ueber die Tage verteilt einen Film nach dem anderen.
        var rule = await fixture.CreateRuleAsync(channel.Id, "Genrefenster", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id,
            episodeCount: 3, genreIds: [genre.Id]);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var collectionIds = new[] { c1.Id, c2.Id, c3.Id };
        var aired = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id)
            .Where(s => s.MovieId is { } id && collectionIds.Contains(id))
            .Select(s => s.MovieId).ToList();

        // Zwei Ausstrahlungen × 1 Sammlungstitel — strikt in Releasedatum-Reihenfolge,
        // andere Titel (Serie, Einzelfilm) duerfen dazwischen liegen.
        Assert.Equal(new long?[] { c1.Id, c2.Id }, aired);

        // Eigener Fortsetzungs-Cursor je Sammlungs-Strang, wie bei den Serien-Strängen.
        var cursor = await fixture.Db.ProgramRuleCursors
            .SingleAsync(c => c.ProgramRuleId == rule.Id && c.StrandKey == $"collection:{collection.Id}", ct);
        Assert.Equal(c2.Id, cursor.LastContentId);
        Assert.Contains(await fixture.Db.ProgramRuleCursors.Where(c => c.ProgramRuleId == rule.Id).ToListAsync(ct),
            c => c.StrandKey == $"show:{show.Id}");
    }

    [Fact]
    public async Task EnsureCoverage_MovieCollection_BlockedHead_DefersInsteadOfSkipping()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("collection-order-defer", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance)
            .UpdateProgramScheduleUnknownRatingModeAsync(ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var collection = await CreateCollectionAsync(fixture, "Reihe");
        // Release-Erster ist FSK-18-gesperrt: ein ungeordneter Ring wuerde ihn mittags
        // ueberspringen und den FSK-0-Film senden — der geordnete Strang laesst den
        // Nachfolger nie vor seinem Vorgaenger laufen und wartet stattdessen.
        var blocked = await fixture.CreateMovieAsync("Teil 1", collectionId: collection.Id,
            duration: TimeSpan.FromMinutes(90), releaseDate: new DateTime(2000, 1, 1), ageRatingMinAge: 18);
        var allowed = await fixture.CreateMovieAsync("Teil 2", collectionId: collection.Id,
            duration: TimeSpan.FromMinutes(90), releaseDate: new DateTime(2001, 1, 1), ageRatingMinAge: 0);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Mittagsplatz", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.MovieCollection, collection.Id);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(Now, ct);

        Assert.DoesNotContain(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == rule.Id);
        // Ein leerer Take schreibt keinen Cursor — der Strang-Kopf wartet.
        Assert.Empty(await fixture.Db.ProgramRuleCursors.Where(c => c.ProgramRuleId == rule.Id).ToListAsync(ct));

        // Im Nachtfenster laeuft die Reihe in ihrer Reihenfolge an — Teil 1 zuerst.
        rule.TimeOfDay = new TimeOnly(23, 30);
        await fixture.Db.SaveChangesAsync(ct);
        await service.EnsureCoverageAsync(Now.AddDays(1), ct);

        var nightSlots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(nightSlots);
        Assert.Equal(blocked.Id, nightSlots[0].MovieId);
    }

    [Fact]
    public async Task Reconcile_OrderedCollectionRing_StaysReproducible()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("collection-order-reconcile", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = await CreateCollectionAsync(fixture, "Reihe");

        for (var i = 0; i < 4; i++)
            await fixture.CreateMovieAsync($"Teil {i + 1}", collectionId: collection.Id,
                duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2000 + i, 1, 1));

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
            episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(Now, ct);
        var summary = await service.ReconcileAsync(Now.AddMinutes(30), ct);

        // Die titelweise Validierung haelt die geplanten Slots gegen denselben
        // Strangaufbau — Ring-Kontiguitaet der geordneten Sammlung eingeschlossen.
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
    }
}
