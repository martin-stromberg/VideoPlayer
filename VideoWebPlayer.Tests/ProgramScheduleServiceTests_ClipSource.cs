using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the clip-source filler material (<see cref="ProgramRuleMaterialType.MediaSourceClips"/>):
/// unclassified video media items of a media source (e.g. mascot clips) rotate as gap filler —
/// without ever having been classified as movies or episodes.
/// </summary>
public sealed class ProgramScheduleServiceTests_ClipSource
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    private static readonly DateTimeOffset Now = new(LocalUtc(2026, 10, 24, 12, 0));

    [Fact]
    public async Task EnsureCoverage_ClipSourceFiller_FillsGapsWithBareMediaItems()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-clip-fill", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var clipA = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        var clipB = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));

        await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var slots = await fixture.SlotsAsync(ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(ProgramRuleType.Filler, s.RuleType));
        Assert.All(slots, s =>
        {
            Assert.Null(s.MovieId);
            Assert.Null(s.TVShowEpisodeId);
            Assert.Contains(s.MediaItemId, new[] { clipA.Id, clipB.Id });
        });
        Assert.Equal(slots.Count, summary.FillerSlotsCreated);
        ProgramScheduleTestDb.AssertGapless(slots, Now.UtcDateTime, Now.UtcDateTime.AddHours(48));
    }

    [Fact]
    public async Task EnsureCoverage_ClipSource_ExcludesClassifiedItems()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-clip-classified", ct);
        var channel = await fixture.GetChannelAsync(ct);
        // A classified movie + an episode item in the same source must not enter the clip ring.
        var movie = await fixture.CreateMovieAsync("Normaler Film", sourceId: 1, duration: TimeSpan.FromMinutes(90));
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 1, sourceId: 1, duration: TimeSpan.FromMinutes(40));
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(2));

        await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var slots = await fixture.SlotsAsync(ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(clip.Id, s.MediaItemId));
    }

    [Fact]
    public async Task EnsureCoverage_ClipSource_SkipsItemsWithoutDurationAndNonVideos()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-clip-noduration", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(2));
        var unprobed = await fixture.CreateMediaItemAsync(1, null);

        // A non-video file in the clip source is never a filler candidate.
        var collection = new MediaCollection { Name = "clips", MediaSourceId = 1, Path = "/clips", CreatedAt = DateTime.UtcNow };
        fixture.Db.MediaCollections.Add(collection);
        await fixture.SaveDiagAsync();
        fixture.Db.MediaItems.Add(new MediaItem
        {
            Name = "readme.txt",
            Path = "/clips/readme.txt",
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromMinutes(5),
            DurationProbedAt = DateTime.UtcNow
        });
        await fixture.SaveDiagAsync();

        await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.True(summary.SkippedNoDuration >= 1);
        var slots = await fixture.SlotsAsync(ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(clip.Id, s.MediaItemId));
    }

    [Fact]
    public async Task EnsureCoverage_ClipSource_NotFullySharedSource_YieldsNoSlots()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-clip-access", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var userA = await fixture.AddUserAsync("a@test.com");
        var userB = await fixture.AddUserAsync("b@test.com");
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(2));

        // Only user A may see source 1 → not fully released for the whole audience.
        await fixture.GrantSourceAccessAsync(userA.Id, 1);

        await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.True(summary.SkippedNoAccess >= 1);
        Assert.Empty(await fixture.SlotsAsync(ct));

        // Once the source is shared with every non-admin user the clips become schedulable.
        await fixture.GrantSourceAccessAsync(userB.Id, 1);
        var summary2 = await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        Assert.True(summary2.FillerSlotsCreated > 0);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.MediaItemId == clip.Id);
    }

    [Fact]
    public async Task EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-clip-rotation", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var clipA = await fixture.CreateMediaItemAsync(1, TimeSpan.FromHours(1));
        var clipB = await fixture.CreateMediaItemAsync(1, TimeSpan.FromHours(1));

        var rule = await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        var cursor = await fixture.Db.ProgramRuleCursors.SingleAsync(c => c.ProgramRuleId == rule.Id, ct);
        Assert.Equal(ProgramCursorContentType.MediaItem, cursor.LastContentType);
        var slots1 = await fixture.SlotsAsync(ct);
        Assert.Equal(slots1[^1].MediaItemId, cursor.LastContentId);

        // Second run extends the coverage: the ring continues where the cursor left off —
        // with exactly two clips it must alternate at the seam.
        var now2 = Now.AddHours(48);
        await fixture.CreateService().EnsureCoverageAsync(now2, ct);
        var slots2 = await fixture.SlotsAsync(ct);
        var firstNew = slots2.First(s => s.Start >= Now.UtcDateTime.AddHours(48));
        var lastBefore = slots2.Last(s => s.Start < Now.UtcDateTime.AddHours(48));
        Assert.NotEqual(lastBefore.MediaItemId, firstNew.MediaItemId);
    }

    [Fact]
    public async Task EnsureCoverage_ClipSource_MissingSource_IsOrphaned()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-clip-missing", ct);
        var channel = await fixture.GetChannelAsync(ct);

        await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 999, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        Assert.Equal(1, summary.OrphanedRules);
        Assert.Empty(await fixture.SlotsAsync(ct));
    }

    [Fact]
    public async Task Reconcile_ClipSourceFillerSlots_StayScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-clip-reconcile", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(2));

        await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var before = await fixture.SlotsAsync(ct);
        Assert.NotEmpty(before);

        var summary = await fixture.CreateService().ReconcileAsync(Now, ct);

        var after = await fixture.SlotsAsync(ct);
        Assert.All(after, s => Assert.Equal(ProgramSlotStatus.Scheduled, s.Status));
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task GetRuleDiagnoses_ClipSource_CountsProbePending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-clip-diagnosis", ct);
        var admin = await fixture.AddUserAsync("admin@x.y", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(2));
        await fixture.CreateMediaItemAsync(1, null); // probe pending

        var rule = await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        var diagnoses = await fixture.CreateService().GetRuleDiagnosesAsync(admin.Id, channel.Id, ct);

        var d = diagnoses[rule.Id];
        Assert.False(d.MaterialMissing);
        Assert.Equal(2, d.CandidateCount);
        Assert.Equal(1, d.SchedulableCount);
        Assert.Equal(1, d.ProbePendingCount);
    }
}
