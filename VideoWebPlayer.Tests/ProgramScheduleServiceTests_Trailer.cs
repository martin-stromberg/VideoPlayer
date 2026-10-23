using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramScheduleServiceTests_Trailer
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<MediaItem> LinkTrailerAsync(ProgramScheduleTestDb fixture, Movie movie, TimeSpan? duration)
    {
        var item = await fixture.CreateMediaItemAsync(duration: duration);
        fixture.Db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id, IsTrailer = true });
        await fixture.SaveDiagAsync();
        return item;
    }

    [Fact]
    public async Task EnsureCoverage_TrailerFiller_AirsTrailerBeforeScheduledMovie()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-trailer-filler", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Kinofilm", duration: TimeSpan.FromMinutes(60));
        var trailerItem = await LinkTrailerAsync(fixture, movie, TimeSpan.FromMinutes(2));
        var fallback = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Trailer", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Trailer, 0, priority: 10, episodeCount: 1);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, fallback.Id, priority: 1, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var trailerSlots = slots.Where(s => s.MediaItemId == trailerItem.Id).ToList();
        Assert.NotEmpty(trailerSlots);
        var movieStarts = slots.Where(s => s.MovieId == movie.Id && s.MediaItemId != trailerItem.Id).Select(s => s.Start).ToList();
        Assert.NotEmpty(movieStarts);
        Assert.All(trailerSlots, s =>
        {
            Assert.Equal(ProgramRuleType.Filler, s.RuleType);
            Assert.Equal(movie.Id, s.MovieId);
            // Vorlauf: der Film des Trailers startet innerhalb der nächsten 7 Tage nach dem Slot.
            Assert.Contains(movieStarts, m => m > s.Start && m <= s.Start.AddDays(7));
        });
        Assert.True(summary.FillerSlotsCreated > 0);
        ProgramScheduleTestDb.AssertGapless(slots, now.UtcDateTime, now.UtcDateTime.AddHours(48));
    }

    [Fact]
    public async Task EnsureCoverage_TrailerOfUnscheduledMovie_IsNotAired()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-trailer-unscheduled", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var unscheduled = await fixture.CreateMovieAsync("Nicht eingeplant", duration: TimeSpan.FromMinutes(90));
        var trailerItem = await LinkTrailerAsync(fixture, unscheduled, TimeSpan.FromMinutes(2));
        var fillerMovie = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Trailer", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Trailer, 0, priority: 10, episodeCount: 1);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, fillerMovie.Id, priority: 1, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        Assert.DoesNotContain(slots, s => s.MediaItemId == trailerItem.Id);
        Assert.NotEmpty(slots);
    }

    [Fact]
    public async Task EnsureCoverage_BeyondLeadWindow_FallsBackToNormalFiller()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-trailer-lead", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Fernfilm", duration: TimeSpan.FromMinutes(60));
        var trailerItem = await LinkTrailerAsync(fixture, movie, TimeSpan.FromMinutes(2));
        var fillerMovie = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Trailer", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Trailer, 0, priority: 10, episodeCount: 1);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, fillerMovie.Id, priority: 1, episodeCount: 1);

        // Lead = 1 day: past the last in-window airing the trailer no longer qualifies and the
        // normal filler covers the tail.
        var service = fixture.CreateService(Microsoft.Extensions.Options.Options.Create(
            new VideoWebPlayer.Configuration.ProgramScheduleOptions { TrailerLeadDays = 1 }));
        var summary = await service.EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        Assert.Equal(0, summary.RemainingGapSeconds);
        var lastAiring = slots.Where(s => s.MovieId == movie.Id && s.MediaItemId != trailerItem.Id).Max(s => s.Start);
        Assert.All(slots.Where(s => s.Start >= lastAiring), s => Assert.NotEqual(trailerItem.Id, s.MediaItemId));
        Assert.Contains(slots, s => s.MovieId == fillerMovie.Id);
        Assert.Contains(slots, s => s.MediaItemId == trailerItem.Id);
    }

    [Fact]
    public async Task EnsureCoverage_TrailerWithoutDuration_IsNotAired()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-trailer-noduration", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Kinofilm", duration: TimeSpan.FromMinutes(60));
        var trailerItem = await LinkTrailerAsync(fixture, movie, duration: null);
        var fillerMovie = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Trailer", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Trailer, 0, priority: 10, episodeCount: 1);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, fillerMovie.Id, priority: 1, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        Assert.DoesNotContain(slots, s => s.MediaItemId == trailerItem.Id);
        Assert.Contains(slots, s => s.MovieId == fillerMovie.Id);
    }

    [Fact]
    public async Task Reconcile_KeptTrailerSlot_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-trailer-reconcile", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Kinofilm", duration: TimeSpan.FromMinutes(60));
        var trailerItem = await LinkTrailerAsync(fixture, movie, TimeSpan.FromMinutes(2));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        var trailerRule = await fixture.CreateRuleAsync(channel.Id, "Trailer", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Trailer, 0, priority: 10, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);
        var summary = await fixture.CreateService().ReconcileAsync(now.AddMinutes(15), ct);

        var slots = await fixture.SlotsAsync(ct);
        var trailerSlots = slots.Where(s => s.MediaItemId == trailerItem.Id).ToList();
        Assert.NotEmpty(trailerSlots);
        Assert.All(trailerSlots, s => Assert.Equal(ProgramSlotStatus.Scheduled, s.Status));
        Assert.True(trailerSlots.All(s => s.ProgramRuleId == trailerRule.Id));
    }
}
