using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Issue „Senderplanung fehlerhaft", scenario 1.i: the reserved "Trailer" clip group.
/// A scheduled movie's trailer airs in the lead window BEFORE that movie; a trailer
/// of a movie that is never scheduled must never appear in the plan.
/// </summary>
public sealed class ProgramScheduleServiceTests_PrimetimeTrailer
{
    private static readonly DateOnly Day = new(2026, 11, 2);

    [Fact]
    public async Task EnsureCoverage_TrailerClipGroup_AirsTrailerBeforeItsMovie()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-trailer", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Action");
        var movie = await fixture.CreateMovieAsync("Blockbuster", duration: TimeSpan.FromMinutes(110));
        await fixture.LinkMovieGenreAsync(movie.Id, genre.Id);
        var second = await fixture.CreateMovieAsync("Nebenrolle", duration: TimeSpan.FromMinutes(105));
        await fixture.LinkMovieGenreAsync(second.Id, genre.Id);
        // A movie that is never scheduled — its trailer must never air.
        var unscheduled = await fixture.CreateMovieAsync("Nie im Plan", duration: TimeSpan.FromMinutes(100));

        var trailer = await PrimetimeSeed.AddTrailerAsync(fixture, movie, TimeSpan.FromMinutes(3));
        var orphanTrailer = await PrimetimeSeed.AddTrailerAsync(fixture, unscheduled, TimeSpan.FromMinutes(3));
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 4, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [genre.Id], [clips.Group.Id, ProgramClipGroup.TrailerGroupId]);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();

        // The trailer of the scheduled movie airs — and always before one of its airings.
        var trailerSlots = ruleSlots.Where(s => s.MediaItemId == trailer.Id).ToList();
        Assert.NotEmpty(trailerSlots);
        var movieStarts = ruleSlots.Where(s => s.MovieId == movie.Id && !s.IsClipFill).Select(s => s.Start).ToList();
        Assert.All(trailerSlots, t => Assert.True(
            movieStarts.Any(m => m > t.Start),
            $"Trailer {PrimetimeSeed.ToLocal(t.Start):dd.MM. HH:mm} ohne folgende Ausstrahlung seines Films."));

        // Negative space: the unscheduled movie's trailer never appears anywhere.
        Assert.DoesNotContain(ruleSlots, s => s.MediaItemId == orphanTrailer.Id);
    }
}
