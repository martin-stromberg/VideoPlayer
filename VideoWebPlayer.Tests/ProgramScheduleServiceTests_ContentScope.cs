using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Integration tests for the program-schedule content scope ("Inhaltsspektrum"): under
/// <see cref="ProgramScheduleContentScope.AllContent"/> the audience-release check of
/// <see cref="ProgramScheduleService"/> is lifted for every expansion path — titles (movies,
/// episodes), the clip-source filler, clip groups and trailers — while the
/// <see cref="ProgramScheduleContentScope.SharedOnly"/> default keeps the previous behaviour.
/// </summary>
public sealed class ProgramScheduleServiceTests_ContentScope
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    private static readonly DateTimeOffset Now = new(LocalUtc(2026, 10, 24, 12, 0));

    private static Task SetScopeAsync(ProgramScheduleTestDb fixture, ProgramScheduleContentScope scope, CancellationToken ct)
        => new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance)
            .UpdateProgramScheduleContentScopeAsync(scope, ct);

    [Fact]
    public async Task EnsureCoverage_AllContent_SchedulesUnreleasedMovieAndEpisode()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("scope-titles", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var userA = await fixture.AddUserAsync("a@test.com");
        await fixture.AddUserAsync("b@test.com");
        var movie = await fixture.CreateMovieAsync("Eingeschränkt", sourceId: 1, duration: TimeSpan.FromMinutes(90));
        var (show, _, episodes) = await fixture.CreateShowAsync("Eingeschränkte Serie", 2, sourceId: 1, duration: TimeSpan.FromMinutes(45));

        // Only user A may see source 1 → nothing in source 1 is fully released.
        await fixture.GrantSourceAccessAsync(userA.Id, 1);

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Abendserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(22, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 1);

        await SetScopeAsync(fixture, ProgramScheduleContentScope.AllContent, ct);
        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.Equal(0, summary.SkippedNoAccess);
        var slots = await fixture.SlotsAsync(ct);
        Assert.Contains(slots, s => s.MovieId == movie.Id);
        Assert.Contains(slots, s => s.TVShowEpisodeId == episodes[0].Id);
    }

    [Fact]
    public async Task EnsureCoverage_AllContent_SchedulesClipSourceItems()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("scope-clipsource", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var userA = await fixture.AddUserAsync("a@test.com");
        await fixture.AddUserAsync("b@test.com");
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(2));

        // Only user A may see source 1 → not fully released for the whole audience.
        await fixture.GrantSourceAccessAsync(userA.Id, 1);

        await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        await SetScopeAsync(fixture, ProgramScheduleContentScope.AllContent, ct);
        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.Equal(0, summary.SkippedNoAccess);
        Assert.True(summary.FillerSlotsCreated > 0);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.MediaItemId == clip.Id);
    }

    [Fact]
    public async Task EnsureCoverage_AllContent_SchedulesClipGroupItems()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("scope-clipgroup", ct);
        var channel = await fixture.GetChannelAsync(ct);
        // A non-admin viewer without any media-source access → source 1 is not fully released.
        await fixture.AddUserAsync("viewer@t.t");
        var movie = await fixture.CreateMovieAsync("Film A", 1, TimeSpan.FromMinutes(76));
        var group = await fixture.CreateClipGroupAsync("Zwischensequenzen", ct);
        var clips = new List<MediaItem>();
        for (var i = 0; i < 6; i++)
        {
            var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
            clips.Add(clip);
            await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
        }
        var clipIds = clips.Select(c => c.Id).ToHashSet();
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(4), clipGroupIds: [group.Id]);

        await SetScopeAsync(fixture, ProgramScheduleContentScope.AllContent, ct);
        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var slots = await fixture.Db.ProgramSlots.AsNoTracking()
            .Where(s => s.ProgramRuleId == rule.Id && s.Start >= LocalUtc(2026, 10, 24, 20, 0) && s.Start < LocalUtc(2026, 10, 25, 20, 0))
            .OrderBy(s => s.Start).ToListAsync(ct);
        Assert.Contains(slots, s => clipIds.Contains(s.MediaItemId));
    }

    [Fact]
    public async Task EnsureCoverage_AllContent_SchedulesTrailers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("scope-trailers", ct);
        var channel = await fixture.GetChannelAsync(ct);
        // A non-admin viewer without any media-source access → source 1 is not fully released.
        await fixture.AddUserAsync("viewer@t.t");
        var movie = await fixture.CreateMovieAsync("Kinofilm", sourceId: 1, duration: TimeSpan.FromMinutes(60));
        var trailerItem = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(2));
        fixture.Db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = trailerItem.Id, IsTrailer = true });
        await fixture.Db.SaveChangesAsync(ct);

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Trailer", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Trailer, 0, priority: 10, episodeCount: 1);

        await SetScopeAsync(fixture, ProgramScheduleContentScope.AllContent, ct);
        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var trailerSlots = (await fixture.SlotsAsync(ct)).Where(s => s.MediaItemId == trailerItem.Id).ToList();
        Assert.NotEmpty(trailerSlots);
        Assert.All(trailerSlots, s => Assert.Equal(movie.Id, s.MovieId));
    }

    [Fact]
    public async Task EnsureCoverage_SharedOnlyDefault_StillFiltersUnreleased()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("scope-sharedonly", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var userA = await fixture.AddUserAsync("a@test.com");
        await fixture.AddUserAsync("b@test.com");
        var movie = await fixture.CreateMovieAsync("Eingeschränkt", sourceId: 1, duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", sourceId: 1, duration: TimeSpan.FromMinutes(30));

        // Only user A may see source 1 → nothing in source 1 is fully released.
        await fixture.GrantSourceAccessAsync(userA.Id, 1);

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        // Explicitly persisted SharedOnly must behave exactly like the untouched default.
        await SetScopeAsync(fixture, ProgramScheduleContentScope.SharedOnly, ct);
        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.True(summary.SkippedNoAccess >= 1);
        Assert.Empty(await fixture.SlotsAsync(ct));
    }

    [Fact]
    public async Task EnsureCoverage_ScopeSwitch_TakesEffectNextRun()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("scope-switch", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var userA = await fixture.AddUserAsync("a@test.com");
        await fixture.AddUserAsync("b@test.com");
        var movie = await fixture.CreateMovieAsync("Eingeschränkt", sourceId: 1, duration: TimeSpan.FromMinutes(90));
        await fixture.GrantSourceAccessAsync(userA.Id, 1);

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        var restrictive = await service.EnsureCoverageAsync(Now, ct);
        Assert.True(restrictive.SkippedNoAccess >= 1);
        Assert.Empty(await fixture.SlotsAsync(ct));

        // The changed setting takes effect on the next planning run — not retroactively.
        await SetScopeAsync(fixture, ProgramScheduleContentScope.AllContent, ct);
        var permissive = await service.EnsureCoverageAsync(Now, ct);

        Assert.Equal(0, permissive.SkippedNoAccess);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.MovieId == movie.Id);
    }

    [Fact]
    public async Task Reconcile_SwitchBackToSharedOnly_DisplacesUnreleasedSlots()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("scope-switchback", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var userA = await fixture.AddUserAsync("a@test.com");
        var userB = await fixture.AddUserAsync("b@test.com");
        // Source 1 is fully shared (filler content), source 2 is only visible to user A.
        var blocked = await fixture.CreateMovieAsync("Gesperrt", sourceId: 2, duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", sourceId: 1, duration: TimeSpan.FromMinutes(15));
        await fixture.GrantSourceAccessAsync(userA.Id, 1);
        await fixture.GrantSourceAccessAsync(userB.Id, 1);
        await fixture.GrantSourceAccessAsync(userA.Id, 2);

        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, blocked.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        await SetScopeAsync(fixture, ProgramScheduleContentScope.AllContent, ct);
        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(Now, ct);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.MovieId == blocked.Id);

        // Switching back to the restrictive scope makes the unreleased slots irreproducible —
        // they are displaced and the freed windows are replanned with released filler content.
        await SetScopeAsync(fixture, ProgramScheduleContentScope.SharedOnly, ct);
        var summary = await service.ReconcileAsync(Now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var slots = await fixture.SlotsAsync(ct);
        Assert.Contains(slots, s => s.MovieId == blocked.Id && s.Status == ProgramSlotStatus.Displaced);
        Assert.DoesNotContain(slots, s => s.MovieId == blocked.Id && s.Status == ProgramSlotStatus.Scheduled);
        var scheduled = slots.Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();
        ProgramScheduleTestDb.AssertGapless(scheduled, Now.UtcDateTime.AddMinutes(30), Now.UtcDateTime.AddHours(48));
    }

    [Fact]
    public async Task GetRuleDiagnoses_AllContent_ReportsNoAccessExclusions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("scope-diagnosis", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        // One non-admin viewer: source 1 is fully shared, source 2 is not.
        var viewer = await fixture.AddUserAsync("viewer@test.com");
        await fixture.GrantSourceAccessAsync(viewer.Id, 1);
        var blocked = await fixture.CreateMovieAsync("Gesperrt", sourceId: 2, duration: TimeSpan.FromMinutes(90));

        var blockedRule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, blocked.Id);

        await SetScopeAsync(fixture, ProgramScheduleContentScope.AllContent, ct);
        var diagnoses = await fixture.CreateService().GetRuleDiagnosesAsync(admin.Id, channel.Id, ct);

        Assert.Equal(0, diagnoses[blockedRule.Id].NoAccessCount);
        Assert.Equal(1, diagnoses[blockedRule.Id].SchedulableCount);
    }
}
