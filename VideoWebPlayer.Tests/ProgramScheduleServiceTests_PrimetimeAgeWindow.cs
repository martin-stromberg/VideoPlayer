using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Issue „Senderplanung fehlerhaft", scenario group 1.g/1.h: the youth-protection
/// window inside the primetime period (defaults: FSK 16 from 22:00, FSK 18 from
/// 23:00, night windows end 06:00) and the treatment of titles without rating under
/// both <see cref="ProgramScheduleUnknownRatingMode"/> settings.
/// </summary>
public sealed class ProgramScheduleServiceTests_PrimetimeAgeWindow
{
    private static readonly DateOnly Day = new(2026, 11, 2);
    private static readonly TimeOnly Fsk18Start = new(23, 0);
    private static readonly TimeOnly NightEnd = new(6, 0);

    private static bool InsideNightWindow(DateTime utc)
    {
        var local = TimeOnly.FromDateTime(PrimetimeSeed.ToLocal(utc));
        return local >= Fsk18Start || local < NightEnd;
    }

    // Scenario 1.g, unordered ring: an FSK-18 movie may never start before its window —
    // the ring skips it while blocked and may only take it once a start ≥ 23:00 is legal.
    [Fact]
    public async Task EnsureCoverage_Fsk18Movie_NeverStartsOutsideNightWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-fsk18-movie", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Action");
        var rated = await fixture.CreateMovieAsync("Familienfilm", duration: TimeSpan.FromMinutes(100), ageRatingMinAge: 0);
        await fixture.LinkMovieGenreAsync(rated.Id, genre.Id);
        var adult = await fixture.CreateMovieAsync("Erwachsenenfilm", duration: TimeSpan.FromMinutes(100), ageRatingMinAge: 18);
        await fixture.LinkMovieGenreAsync(adult.Id, genre.Id);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id, [genre.Id], []);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        var adultSlots = ruleSlots.Where(s => s.MovieId == adult.Id).ToList();

        // The FSK-18 title may air — but never with a start outside its night window.
        Assert.All(adultSlots, s => Assert.True(InsideNightWindow(s.Start),
            $"FSK-18-Start {PrimetimeSeed.ToLocal(s.Start):dd.MM. HH:mm} außerhalb des Nachtfensters."));
        // And every evening still carries titles (the allowed movie fills in).
        PrimetimeSeed.AssertEveryEveningHasTitles(ruleSlots, Day, PrimetimeSeed.HorizonDays);
    }

    // Scenario 1.g, ordered strand: an FSK-18 show's blocked head episode defers the
    // whole strand for the airing — air order is never broken to serve a later episode.
    [Fact]
    public async Task EnsureCoverage_Fsk18Show_BlockedHeadKeepsAirOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-fsk18-show", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (gMovies, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Action", 6, TimeSpan.FromMinutes(100), minAge: 0);
        var (gShow, _, episodes) = await PrimetimeSeed.AddShowGenreAsync(fixture, "Erwachsenenserie", "Blutrausch", 5, TimeSpan.FromMinutes(45), minAge: 18);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id, [gMovies.Id, gShow.Id], []);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        var episodeIds = episodes.Select(e => e.Id).ToHashSet();
        var showSlots = ruleSlots.Where(s => s.TVShowEpisodeId is { } id && episodeIds.Contains(id)).ToList();

        // No episode of the blocked show may ever start outside the night window…
        Assert.All(showSlots, s => Assert.True(InsideNightWindow(s.Start),
            $"Episode startet {PrimetimeSeed.ToLocal(s.Start):dd.MM. HH:mm} außerhalb des Nachtfensters."));
        // …and air order stays a continuous rotation: each placed episode follows its
        // predecessor (+1) or wraps to the first — never a jump.
        var placed = showSlots.Select(s => episodes.Single(e => e.Id == s.TVShowEpisodeId).Number).ToList();
        for (var i = 1; i < placed.Count; i++)
        {
            var expected = placed[i - 1] == episodes.Count ? 1 : placed[i - 1] + 1;
            Assert.True(placed[i] == expected,
                $"Episodensprung: {placed[i - 1]} → {placed[i]} an Position {i}.");
        }
    }

    // Scenario 1.h, mode Unrestricted: a title without rating may start at 20:15.
    [Fact]
    public async Task EnsureCoverage_UnratedMovie_Unrestricted_StartsInPrimetime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-unrated-open", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var setup = await fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleUnknownRatingMode = ProgramScheduleUnknownRatingMode.Unrestricted;
        await fixture.Db.SaveChangesAsync(ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Action");
        var unrated = await fixture.CreateMovieAsync("Ohne Freigabe", duration: TimeSpan.FromMinutes(100));
        await fixture.LinkMovieGenreAsync(unrated.Id, genre.Id);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id, [genre.Id], []);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var tonight = PrimetimeSeed.AiringOf(
            (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id), Day);
        Assert.Contains(tonight, s => s.MovieId == unrated.Id && !s.IsClipFill);
    }

    // Scenario 1.h, mode RestrictToNightWindow: the same unrated title may never start
    // before the night window — it can only air once a start ≥ 23:00 is legal.
    [Fact]
    public async Task EnsureCoverage_UnratedMovie_Restricted_NeverStartsBeforeNightWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-unrated-restricted", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var setup = await fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleUnknownRatingMode = ProgramScheduleUnknownRatingMode.RestrictToNightWindow;
        await fixture.Db.SaveChangesAsync(ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Action");
        // 60-minute rated titles: starts at 20:15, 21:15, 22:15 are all < 23:00 —
        // the unrated title's first legal start is 23:15.
        var rated = await fixture.CreateMovieAsync("Familienfilm", duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 0);
        await fixture.LinkMovieGenreAsync(rated.Id, genre.Id);
        var unrated = await fixture.CreateMovieAsync("Ohne Freigabe", duration: TimeSpan.FromMinutes(100));
        await fixture.LinkMovieGenreAsync(unrated.Id, genre.Id);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id, [genre.Id], []);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var tonight = PrimetimeSeed.AiringOf(
            (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id), Day);
        var unratedSlots = tonight.Where(s => s.MovieId == unrated.Id).ToList();
        Assert.All(unratedSlots, s => Assert.True(InsideNightWindow(s.Start),
            $"Titel ohne Altersangabe startet {PrimetimeSeed.ToLocal(s.Start):dd.MM. HH:mm} vor dem Nachtfenster."));
    }
}
