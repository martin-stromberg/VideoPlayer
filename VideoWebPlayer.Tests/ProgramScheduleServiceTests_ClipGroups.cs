using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for placeholder clip groups: a per-rule grace stops title selection near the period
/// end of a slot-duration rule and the remainder is filled with clip slots distributed across
/// the n+1 segments of the airing (before, between and after the titles).
/// </summary>
public sealed class ProgramScheduleServiceTests_ClipGroups
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    private static readonly DateTimeOffset Now = new(LocalUtc(2026, 10, 24, 12, 0));

    // Rule daily 20:00 with 4h slot, material = one movie of 76 min → three titles end at
    // 23:48, the 12-minute rest (< 15 min grace) goes to clip fill across 4 segments of 3 min.
    private static async Task<(ProgramRule rule, Movie movie, ProgramClipGroup? group, List<MediaItem> clips)> SeedClipAiringAsync(
        ProgramScheduleTestDb fixture, CancellationToken ct,
        TimeSpan? clipDuration = null, TimeSpan? grace = null, TimeSpan? slotDuration = null,
        int clipCount = 6, bool withGroup = true)
    {
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film A", 1, TimeSpan.FromMinutes(76));
        ProgramClipGroup? group = null;
        var clips = new List<MediaItem>();
        if (withGroup)
        {
            group = await fixture.CreateClipGroupAsync("Zwischensequenzen", ct);
            for (var i = 0; i < clipCount; i++)
            {
                var clip = await fixture.CreateMediaItemAsync(1, clipDuration ?? TimeSpan.FromSeconds(60));
                clips.Add(clip);
                await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
            }
        }
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: slotDuration ?? TimeSpan.FromHours(4),
            clipGroupIds: group is null ? null : [group.Id],
            placeholderGrace: grace ?? TimeSpan.FromMinutes(15));
        return (rule, movie, group, clips);
    }

    private static async Task<List<ProgramSlot>> AiringSlotsAsync(ProgramScheduleTestDb fixture, ProgramRule rule, CancellationToken ct)
        => await fixture.Db.ProgramSlots.AsNoTracking()
            .Where(s => s.ProgramRuleId == rule.Id && s.Start >= LocalUtc(2026, 10, 24, 20, 0) && s.Start < LocalUtc(2026, 10, 25, 20, 0))
            .OrderBy(s => s.Start).ToListAsync(ct);

    [Fact]
    public async Task EnsureCoverage_GraceRemainder_FillsWithDistributedClips()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-core", ct);
        var (rule, _, _, clips) = await SeedClipAiringAsync(fixture, ct, clipCount: 12);
        var clipIds = clips.Select(c => c.Id).ToHashSet();

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);

        var titles = slots.Where(s => !clipIds.Contains(s.MediaItemId)).ToList();
        var clipSlots = slots.Where(s => clipIds.Contains(s.MediaItemId)).ToList();
        Assert.Equal(3, titles.Count);
        Assert.Equal(12, clipSlots.Count);
        Assert.All(clipSlots, s =>
        {
            Assert.Null(s.MovieId);
            Assert.Null(s.TVShowEpisodeId);
            Assert.Equal(rule.RuleType, s.RuleType);
        });
        // Verteilt: Titel an Position 3, 7, 11 (3 Clips je Segment).
        Assert.Equal(new[] { 3, 7, 11 }, titles.Select(t => slots.IndexOf(t)).ToArray());
        // Lückenlos und Ende am Periodenende 24:00.
        for (var i = 1; i < slots.Count; i++)
            Assert.Equal(slots[i - 1].End, slots[i].Start);
        Assert.Equal(LocalUtc(2026, 10, 24, 20, 0) + TimeSpan.FromHours(4), slots[^1].End);
    }

    [Fact]
    public async Task EnsureCoverage_RemainderAboveGrace_StartsNextTitle_Overshoots()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-above-grace", ct);
        var (rule, _, _, clips) = await SeedClipAiringAsync(fixture, ct, grace: TimeSpan.FromMinutes(10));
        var clipIds = clips.Select(c => c.Id).ToHashSet();

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);

        Assert.Equal(4, slots.Count(s => !clipIds.Contains(s.MediaItemId)));
        Assert.DoesNotContain(slots, s => clipIds.Contains(s.MediaItemId));
        Assert.True(slots[^1].End > LocalUtc(2026, 10, 24, 20, 0) + TimeSpan.FromHours(4)); // Überschuss wie bisher
    }

    [Fact]
    public async Task EnsureCoverage_RemainderExactlyGrace_StartsNextTitle()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-exact-grace", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film B", 1, TimeSpan.FromMinutes(75));
        var group = await fixture.CreateClipGroupAsync("G", ct);
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
        await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "R", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(4), clipGroupIds: [group.Id], placeholderGrace: TimeSpan.FromMinutes(15));

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);

        Assert.Equal(4, slots.Count(s => s.MediaItemId != clip.Id)); // Rest == Grace → Titel startet
        Assert.DoesNotContain(slots, s => s.MediaItemId == clip.Id);
    }

    [Fact]
    public async Task EnsureCoverage_NoGroups_UnchangedOvershootBehavior()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-nogroups", ct);
        var (rule, _, _, _) = await SeedClipAiringAsync(fixture, ct, withGroup: false);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);

        Assert.Equal(4, slots.Count); // 4 Titel, Overshoot, keine Clips
        Assert.True(slots[^1].End > LocalUtc(2026, 10, 24, 20, 0) + TimeSpan.FromHours(4));
    }

    [Fact]
    public async Task EnsureCoverage_LastClip_MayOvershootPeriodEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-overshoot", ct);
        var (rule, _, _, clips) = await SeedClipAiringAsync(fixture, ct, clipDuration: TimeSpan.FromSeconds(200));
        var clipIds = clips.Select(c => c.Id).ToHashSet();

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);
        var clipSlots = slots.Where(s => clipIds.Contains(s.MediaItemId)).ToList();

        Assert.NotEmpty(clipSlots);
        Assert.True(slots[^1].End > LocalUtc(2026, 10, 24, 20, 0) + TimeSpan.FromHours(4), "letzter Clip darf überragen");
    }

    [Fact]
    public async Task EnsureCoverage_ClipTooLargeForFreeRoom_LeavesDocumentedGap()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-toolarge", ct);
        var (rule, _, _, clips) = await SeedClipAiringAsync(fixture, ct, clipDuration: TimeSpan.FromMinutes(20));
        var clipIds = clips.Select(c => c.Id).ToHashSet();

        var summary = await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);

        Assert.DoesNotContain(slots, s => clipIds.Contains(s.MediaItemId));
        Assert.True(summary.RemainingGapSeconds > 0);
    }

    [Fact]
    public async Task EnsureCoverage_ClipsSkipItemsWithoutDuration()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-noduration", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film C", 1, TimeSpan.FromMinutes(76));
        var group = await fixture.CreateClipGroupAsync("G", ct);
        var noDur = await fixture.CreateMediaItemAsync(1, null);
        await fixture.AddClipToGroupAsync(group.Id, noDur.Id, ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "R", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(4), clipGroupIds: [group.Id]);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);
        Assert.DoesNotContain(slots, s => s.MediaItemId == noDur.Id);
    }

    [Fact]
    public async Task EnsureCoverage_ClipGroup_NotFullySharedSource_YieldsNoClipSlots()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-noaccess", ct);
        await fixture.AddUserAsync("viewer@t.t", isAdmin: false);
        var (rule, _, _, clips) = await SeedClipAiringAsync(fixture, ct);
        var clipIds = clips.Select(c => c.Id).ToHashSet();
        // Quelle 1 ist für den Nicht-Admin nicht freigegeben → Clip-Expansion leer.

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);
        Assert.DoesNotContain(slots, s => clipIds.Contains(s.MediaItemId));
    }

    [Fact]
    public async Task EnsureCoverage_TwoClipGroups_RoundRobinDistributed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-roundrobin", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film RR", 1, TimeSpan.FromMinutes(76));
        var groupA = await fixture.CreateClipGroupAsync("A", ct);
        var groupB = await fixture.CreateClipGroupAsync("B", ct);
        var idsA = new HashSet<long>();
        var idsB = new HashSet<long>();
        for (var i = 0; i < 8; i++)
        {
            var a = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
            idsA.Add(a.Id);
            await fixture.AddClipToGroupAsync(groupA.Id, a.Id, ct);
            var b = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
            idsB.Add(b.Id);
            await fixture.AddClipToGroupAsync(groupB.Id, b.Id, ct);
        }
        var rule = await fixture.CreateRuleAsync(channel.Id, "R", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(4), clipGroupIds: [groupA.Id, groupB.Id]);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);
        var clipSeq = slots.Where(s => idsA.Contains(s.MediaItemId) || idsB.Contains(s.MediaItemId)).Select(s => s.MediaItemId).ToList();

        Assert.True(clipSeq.Count >= 6);
        // Streng alternierend A/B über die Ringe.
        for (var i = 0; i + 1 < clipSeq.Count; i++)
        {
            var firstIsA = idsA.Contains(clipSeq[i]);
            var secondIsA = idsA.Contains(clipSeq[i + 1]);
            Assert.NotEqual(firstIsA, secondIsA);
        }
    }

    [Fact]
    public async Task EnsureCoverage_ClipOvershoot_ReservedBoundWindow_ClipDropped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-bound", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (rule, _, _, clips) = await SeedClipAiringAsync(fixture, ct, clipDuration: TimeSpan.FromSeconds(400));
        var clipIds = clips.Select(c => c.Id).ToHashSet();
        // Höherpriore Regel B um 23:55, deren Episode vor der harten Grenze C um 24:10 nicht
        // komplettieren kann → materialisiert nichts, reserviert aber das Bound-Fenster.
        var (show, _, _) = await fixture.CreateShowAsync("B-Show", 3, 1, TimeSpan.FromMinutes(45));
        await fixture.CreateRuleAsync(channel.Id, "B", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(23, 55), ProgramRuleMaterialType.TVShow, show.Id,
            priority: 5, episodeCount: 1);
        var movieC = await fixture.CreateMovieAsync("C", 1, TimeSpan.FromMinutes(30));
        await fixture.CreateRuleAsync(channel.Id, "C", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(0, 10), ProgramRuleMaterialType.Movie, movieC.Id,
            priority: 9, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);
        var clipSlots = slots.Where(s => clipIds.Contains(s.MediaItemId)).ToList();

        // Clip darf nicht in das Bound-Fenster B@23:55 ragen.
        Assert.All(clipSlots, s => Assert.True(s.End <= LocalUtc(2026, 10, 24, 23, 55)));
    }

    [Fact]
    public async Task EnsureCoverage_ClipRing_RotatesAcrossRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-rotation", ct);
        var (rule, _, group, clips) = await SeedClipAiringAsync(fixture, ct, clipCount: 5);
        var clipIds = clips.Select(c => c.Id).ToList();

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(Now, ct);
        var firstDayClips = (await AiringSlotsAsync(fixture, rule, ct))
            .Where(s => clipIds.Contains(s.MediaItemId)).Select(s => s.MediaItemId).ToList();

        var day2 = new DateTimeOffset(LocalUtc(2026, 10, 25, 12, 0));
        await service.EnsureCoverageAsync(day2, ct);
        var secondDayClips = (await fixture.Db.ProgramSlots.AsNoTracking()
                .Where(s => s.ProgramRuleId == rule.Id && s.Start >= LocalUtc(2026, 10, 25, 20, 0) && s.Start < LocalUtc(2026, 10, 26, 20, 0))
                .OrderBy(s => s.Start).ToListAsync(ct))
            .Where(s => clipIds.Contains(s.MediaItemId)).Select(s => s.MediaItemId).ToList();

        Assert.NotEmpty(firstDayClips);
        Assert.NotEmpty(secondDayClips);
        // 12 Entnahmen je Ausstrahlung aus einem 5er-Ring → Tag 2 startet bei Index 2.
        Assert.NotEqual(firstDayClips[0], secondDayClips[0]); // Ring läuft über den Cursor weiter
        var cursor = await fixture.Db.ProgramRuleCursors
            .FirstOrDefaultAsync(c => c.ProgramRuleId == rule.Id && c.StrandKey == $"clipgroup:{group!.Id}", ct);
        Assert.NotNull(cursor);
        Assert.Equal(ProgramCursorContentType.MediaItem, cursor!.LastContentType);
    }

    [Fact]
    public async Task EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-trailer", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (rule, _, _, _) = await SeedClipAiringAsync(fixture, ct, withGroup: false);
        await fixture.Db.ProgramRuleClipGroups.AddAsync(new ProgramRuleClipGroup { ProgramRuleId = rule.Id, ProgramClipGroupId = ProgramClipGroup.TrailerGroupId }, ct);
        await fixture.Db.SaveChangesAsync(ct);
        // Film X wird morgen mittag ausgestrahlt → sein Trailer ist ein Vorlauf-Treffer.
        var movieX = await fixture.CreateMovieAsync("Film X", 1, TimeSpan.FromMinutes(30));
        var trailerItem = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
        fixture.Db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movieX.Id, MediaItemId = trailerItem.Id, IsTrailer = true });
        await fixture.Db.SaveChangesAsync(ct);
        await fixture.CreateRuleAsync(channel.Id, "Mittagsfilm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Movie, movieX.Id,
            priority: 3, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);

        var trailerSlot = slots.FirstOrDefault(s => s.MediaItemId == trailerItem.Id);
        Assert.NotNull(trailerSlot);
        Assert.Equal(movieX.Id, trailerSlot!.MovieId);
    }

    [Fact]
    public async Task EnsureCoverage_TrailerGroup_NoLeadMatch_OtherGroupsFill()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-trailer-none", ct);
        var (rule, _, group, clips) = await SeedClipAiringAsync(fixture, ct);
        await fixture.Db.ProgramRuleClipGroups.AddAsync(new ProgramRuleClipGroup { ProgramRuleId = rule.Id, ProgramClipGroupId = ProgramClipGroup.TrailerGroupId }, ct);
        await fixture.Db.SaveChangesAsync(ct);
        var clipIds = clips.Select(c => c.Id).ToHashSet();

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);
        Assert.Contains(slots, s => clipIds.Contains(s.MediaItemId));
    }

    [Fact]
    public async Task EnsureCoverage_TrailerClip_ExcludesItemsWithoutDuration()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-trailer-nodur", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (rule, _, _, _) = await SeedClipAiringAsync(fixture, ct, withGroup: false);
        await fixture.Db.ProgramRuleClipGroups.AddAsync(new ProgramRuleClipGroup { ProgramRuleId = rule.Id, ProgramClipGroupId = ProgramClipGroup.TrailerGroupId }, ct);
        await fixture.Db.SaveChangesAsync(ct);
        var movieX = await fixture.CreateMovieAsync("Film Y", 1, TimeSpan.FromMinutes(30));
        var trailerItem = await fixture.CreateMediaItemAsync(1, null);
        fixture.Db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movieX.Id, MediaItemId = trailerItem.Id, IsTrailer = true });
        await fixture.Db.SaveChangesAsync(ct);
        await fixture.CreateRuleAsync(channel.Id, "Mittagsfilm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Movie, movieX.Id,
            priority: 3, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);
        Assert.DoesNotContain(slots, s => s.MediaItemId == trailerItem.Id);
    }

    [Fact]
    public async Task EnsureCoverage_EpisodeCountAndSlotDuration_FillsTailWithClips()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-episodes", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 4, 1, TimeSpan.FromMinutes(20));
        var group = await fixture.CreateClipGroupAsync("G", ct);
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
        await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Serie", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, show.Id,
            episodeCount: 1, slotDuration: TimeSpan.FromMinutes(60), clipGroupIds: [group.Id]);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);

        var clipSlots = slots.Where(s => s.MediaItemId == clip.Id).ToList();
        Assert.NotEmpty(clipSlots);
        var periodEnd = LocalUtc(2026, 10, 24, 20, 0) + TimeSpan.FromMinutes(60);
        Assert.True(slots[^1].End >= periodEnd - TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task EnsureCoverage_InterleavedRule_ClipFillDistributed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-interleaved", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Drama", 1);
        var (show1, _, _) = await fixture.CreateShowAsync("S1", 3, 1, TimeSpan.FromMinutes(76));
        var (show2, _, _) = await fixture.CreateShowAsync("S2", 3, 1, TimeSpan.FromMinutes(76));
        await fixture.LinkShowGenreAsync(show1.Id, genre.Id);
        await fixture.LinkShowGenreAsync(show2.Id, genre.Id);
        var group = await fixture.CreateClipGroupAsync("G", ct);
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
        await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Genre", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id,
            slotDuration: TimeSpan.FromHours(4), genreIds: [genre.Id], clipGroupIds: [group.Id]);

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);
        var slots = await AiringSlotsAsync(fixture, rule, ct);

        Assert.Contains(slots, s => s.MediaItemId == clip.Id);
    }

    [Fact]
    public async Task Reconcile_ClipFillSlots_StayScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-reconcile", ct);
        var (rule, _, _, clips) = await SeedClipAiringAsync(fixture, ct, clipCount: 12);
        var clipIds = clips.Select(c => c.Id).ToHashSet();
        var service = fixture.CreateService();

        await service.EnsureCoverageAsync(Now, ct);
        var before = (await AiringSlotsAsync(fixture, rule, ct)).Count;
        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 30)), ct);

        Assert.Equal(0, summary.SlotsDisplaced);
        var after = await AiringSlotsAsync(fixture, rule, ct);
        Assert.Equal(before, after.Count);
        Assert.True(after.Count(s => clipIds.Contains(s.MediaItemId)) > 0);
    }

    [Fact]
    public async Task Reconcile_RemovedClipGroupMember_DisplacesAndReplans()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-reconcile-rem", ct);
        var (rule, _, group, clips) = await SeedClipAiringAsync(fixture, ct, clipCount: 12);
        var clipIds = clips.Select(c => c.Id).ToHashSet();
        var service = fixture.CreateService();

        await service.EnsureCoverageAsync(Now, ct);
        // Mitglied entfernen → Ausstrahlung nicht mehr reproduzierbar → verdrängt + neu geplant.
        fixture.Db.ProgramClipGroupItems.RemoveRange(
            fixture.Db.ProgramClipGroupItems.Where(i => i.ProgramClipGroupId == group!.Id && i.MediaItemId == clips[0].Id));
        await fixture.Db.SaveChangesAsync(ct);

        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 30)), ct);
        Assert.True(summary.SlotsDisplaced > 0);
    }

    [Fact]
    public async Task Reconcile_WorkerLoop_WithClipFill_NoDisplacedAccumulation()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cg-workerloop", ct);
        var (rule, _, _, clips) = await SeedClipAiringAsync(fixture, ct, clipCount: 12);
        var service = fixture.CreateService();

        await service.EnsureCoverageAsync(Now, ct);
        for (var i = 1; i <= 3; i++)
        {
            var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0) + TimeSpan.FromMinutes(15 * i)), ct);
            Assert.Equal(0, summary.SlotsDisplaced);
        }
        var slots = await fixture.SlotsAsync(ct);
        var scheduled = slots.Where(s => s.Status == ProgramSlotStatus.Scheduled).OrderBy(s => s.Start).ToList();
        for (var i = 1; i < scheduled.Count; i++)
            Assert.True(scheduled[i].Start >= scheduled[i - 1].End, "kein Overlap");
    }
}
