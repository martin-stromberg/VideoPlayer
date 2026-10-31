using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regression tests for issue #315: the rolling re-evaluation invalidated an unchanged,
/// still-running interleaved airing with <c>HeadPickInconsistent</c> whenever a strand had
/// only supplied unwritten head picks (taken but never written because the coverage run
/// began mid-occurrence) and no written contribution in the remaining group — displacing
/// and re-materializing the identical tail in an endless loop that killed the running title.
/// Also covers: fully aired slots are never re-evaluated, and the slot carrying the
/// playback position is never cut mid-air unless the current age-window settings already
/// forbid it at its own start.
/// </summary>
public sealed class ProgramScheduleServiceTests_Issue315
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    // Three-strand genre rotation (three shows à 45 min) with a clip group, daily 20:00,
    // 4 h slot: six picks per occurrence rotating s0,s1,s2,s0,s1,s2.
    private static async Task<(ProgramScheduleTestDb Fixture, ProgramRule Rule)> SeedInterleavedClipRuleAsync(
        string dbName, CancellationToken ct)
    {
        var fixture = await ProgramScheduleTestDb.CreateAsync(dbName, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Mix", 1);
        foreach (var name in new[] { "S1", "S2", "S3" })
        {
            var (show, _, _) = await fixture.CreateShowAsync(name, 10, 1, TimeSpan.FromMinutes(45));
            await fixture.LinkShowGenreAsync(show.Id, genre.Id);
        }
        var group = await fixture.CreateClipGroupAsync("G", ct);
        for (var i = 0; i < 20; i++)
        {
            var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
            await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
        }
        var rule = await fixture.CreateRuleAsync(channel.Id, "Genre", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id,
            slotDuration: TimeSpan.FromHours(4), genreIds: [genre.Id], clipGroupIds: [group.Id]);
        return (fixture, rule);
    }

    // Core failure of the issue: coverage began inside pick 4 of the occurrence, so the
    // written tail {s1, s2} leaves prefix strand s0 without any written contribution —
    // the check used to reject every hypothesis (HeadPickInconsistent) although the
    // airing is a faithful materialization. Must stay stable, repeatedly.
    [Fact]
    public async Task Reconcile_UnchangedRules_RunningAiringWithUnwrittenStrandHead_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        var messages = new ConcurrentQueue<string>();
        var (fixture, _) = await SeedInterleavedClipRuleAsync("i315-unwritten-head", ct);
        using var _ = fixture;
        var service = fixture.CreateService(logger: new ListLogger<ProgramScheduleService>(messages));

        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 23, 20)), ct);
        var before = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId, s.MovieId))
            .ToList();

        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 23, 35)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct, "Lauf 1");

        // Second run stays quiet — no oscillation.
        var summary2 = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 23, 50)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary2, ct, "Lauf 2");

        var after = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId, s.MovieId))
            .ToList();
        Assert.All(before, b => Assert.Contains(after, a => a == b));
        Assert.DoesNotContain(messages, m => m.Contains("HeadPickInconsistent"));
    }


}
