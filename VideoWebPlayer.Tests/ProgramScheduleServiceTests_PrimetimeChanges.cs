using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Issue „Senderplanung fehlerhaft", scenario group 1.d/1.e: the material set or the
/// rule's genre selection GROWS mid-simulation. Everything already planned must stay
/// — no displacement, no re-materialization; only new airings may use the new content.
/// </summary>
public sealed class ProgramScheduleServiceTests_PrimetimeChanges
{
    private static async Task<(ProgramRule Rule, Genre MovieGenre, Genre ShowGenre)> SeedBaseAsync(
        ProgramScheduleTestDb fixture, CancellationToken ct)
    {
        var channel = await fixture.GetChannelAsync(ct);
        var (gMovies, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Action", 4, TimeSpan.FromMinutes(100));
        var (gShow, _, _) = await PrimetimeSeed.AddShowGenreAsync(fixture, "Krimi", "Tatort", 8, TimeSpan.FromMinutes(45));
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 4, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [gMovies.Id, gShow.Id], [clips.Group.Id, ProgramClipGroup.TrailerGroupId]);
        return (rule, gMovies, gShow);
    }

    // Scenario 1.d: a further genre (with its own show → a NEW interleaved strand) is
    // added to the rule on day two. Planned airings must not be displaced.
    [Fact]
    public async Task Simulation_GenreAddedToRule_NoDisplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-genre-added", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var (rule, _, _) = await SeedBaseAsync(fixture, ct);

        var day0 = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.StepAsync(fixture, day0, ct);
        var before = await fixture.SlotsAsync(ct);

        // Day two: the rule gains a genre — a new show strand joins the rotation.
        var (gNew, _, _) = await PrimetimeSeed.AddShowGenreAsync(fixture, "Dokus", "Weltreise", 5, TimeSpan.FromMinutes(45));
        fixture.Db.ProgramRuleGenres.Add(new ProgramRuleGenre { ProgramRuleId = rule.Id, GenreId = gNew.Id });
        await fixture.SaveDiagAsync();

        var day1 = day0.AddDays(1);
        var (reconcile, _) = await PrimetimeSeed.StepAsync(fixture, day1, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, reconcile, ct, "Genre hinzugefügt");
        PrimetimeSeed.AssertPreserved(before, await fixture.SlotsAsync(ct), "Genre hinzugefügt");
    }

    // Scenario 1.e: the material set grows — a new movie joins an already selected
    // genre. Planned airings must not be displaced either.
    [Fact]
    public async Task Simulation_MovieAddedToGenre_NoDisplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-movie-added", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var (rule, gMovies, _) = await SeedBaseAsync(fixture, ct);

        var day0 = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.StepAsync(fixture, day0, ct);
        var before = await fixture.SlotsAsync(ct);

        // Day two: a new movie appears in the already-selected genre.
        var movie = await fixture.CreateMovieAsync("Action-Film-Neu", duration: TimeSpan.FromMinutes(100));
        await fixture.LinkMovieGenreAsync(movie.Id, gMovies.Id);

        var day1 = day0.AddDays(1);
        var (reconcile, _) = await PrimetimeSeed.StepAsync(fixture, day1, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, reconcile, ct, "Film hinzugefügt");
        PrimetimeSeed.AssertPreserved(before, await fixture.SlotsAsync(ct), "Film hinzugefügt");
    }

    // Flat-path counterpart of 1.e: a rule carrying only a movie genre (no show genre →
    // SetItems takes the flat path with a single "other" ring) grows a movie on day two.
    // Planned airings must not be displaced either.
    [Fact]
    public async Task Simulation_MovieAddedToFlatGenreRule_NoDisplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-flat-movie-added", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (gMovies, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Action", 4, TimeSpan.FromMinutes(100));
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 4, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [gMovies.Id], [clips.Group.Id, ProgramClipGroup.TrailerGroupId]);

        var day0 = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.StepAsync(fixture, day0, ct);
        var before = await fixture.SlotsAsync(ct);

        // Day two: a new movie appears in the already-selected genre.
        var movie = await fixture.CreateMovieAsync("Action-Film-Neu", duration: TimeSpan.FromMinutes(100));
        await fixture.LinkMovieGenreAsync(movie.Id, gMovies.Id);

        var day1 = day0.AddDays(1);
        var (reconcile, _) = await PrimetimeSeed.StepAsync(fixture, day1, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, reconcile, ct, "Film hinzugefügt (Flachpfad)");
        PrimetimeSeed.AssertPreserved(before, await fixture.SlotsAsync(ct), "Film hinzugefügt (Flachpfad)");
    }
}
