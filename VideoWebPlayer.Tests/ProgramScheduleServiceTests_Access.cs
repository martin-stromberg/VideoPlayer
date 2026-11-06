using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramScheduleServiceTests_Access
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    private static readonly DateTimeOffset Now = new(LocalUtc(2026, 10, 24, 12, 0));

    [Fact]
    public async Task EnsureCoverage_ItemWithoutDuration_IsSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-noduration", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Ohne Laufzeit", duration: null);
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(30));

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.True(summary.SkippedNoDuration >= 1);
        Assert.DoesNotContain(await fixture.SlotsAsync(ct), s => s.MovieId == movie.Id);
    }

    [Fact]
    public async Task EnsureCoverage_NotFullyReleased_IsExcluded()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-access", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var userA = await fixture.AddUserAsync("a@test.com");
        var userB = await fixture.AddUserAsync("b@test.com");
        var movie = await fixture.CreateMovieAsync("Eingeschränkt", sourceId: 1, duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", sourceId: 1, duration: TimeSpan.FromMinutes(30));

        // Only user A may see source 1 → nothing in source 1 is fully released.
        await fixture.GrantSourceAccessAsync(userA.Id, 1);

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.True(summary.SkippedNoAccess >= 1);
        Assert.Empty(await fixture.SlotsAsync(ct));

        // Grant B access too → now everything is fully released.
        await fixture.GrantSourceAccessAsync(userB.Id, 1);
        var summary2 = await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        Assert.True(summary2.SlotsCreated > 0);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.MovieId == movie.Id);
    }

    [Fact]
    public async Task EnsureCoverage_UnlockedForAll_IsScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-unlocked", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var userA = await fixture.AddUserAsync("a@test.com");
        var userB = await fixture.AddUserAsync("b@test.com");
        var (show, _, episodes) = await fixture.CreateShowAsync("Freigeschaltete Serie", 2, sourceId: 1, duration: TimeSpan.FromMinutes(45));

        // No MediaSourceUser at all; the show is individually unlocked for every user.
        fixture.Db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = userA.Id, TVShowId = show.Id });
        fixture.Db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = userB.Id, TVShowId = show.Id });
        await fixture.Db.SaveChangesAsync(ct);

        await fixture.CreateRuleAsync(channel.Id, "Abendserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.True(summary.SlotsCreated > 0);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.TVShowEpisodeId == episodes[0].Id);
    }

    [Fact]
    public async Task EnsureCoverage_NoNonAdminUsers_EverythingSchedulable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-admins-only", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.True(summary.SlotsCreated > 0);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.MovieId == movie.Id);
    }
}
