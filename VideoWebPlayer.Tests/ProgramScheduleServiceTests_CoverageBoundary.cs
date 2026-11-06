using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regression test for the overlap found in the local installation (issue.md, group 3):
/// an airing that ends exactly on the coverage boundary is invisible to the coverage
/// pass — the persisted-occupancy load filters on <c>End &gt; coverageStart</c>, so a
/// chain whose last slot lands on the boundary itself never enters <c>blocked</c>. The
/// lookback then re-materializes the occurrence from its occurrence start: replayed
/// takes position as a fresh airing and a take reaching past the boundary is written
/// inside the persisted airing.
/// </summary>
public sealed class ProgramScheduleServiceTests_CoverageBoundary
{
    /// <summary>A flat genre rotation airs a completed primetime block; the coverage
    /// pass that follows must not append a tail take inside the persisted airing.</summary>
    [Fact]
    public async Task EnsureCoverage_AiringEndingAtCoverageStart_NoOverlapIntoPersistedAiring()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("coverage-boundary-tail", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 2, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (genre, movies) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Action", 8, TimeSpan.FromMinutes(100));
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 4, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [genre.Id], [clips.Group.Id]);

        // Persisted airing of the Nov-3 occurrence as the worker wrote it: three
        // contiguous slots from 20:15 local, the last one ending at 23:18:45 UTC —
        // the exact boundary the coverage start will land on.
        var itemIds = await fixture.Db.MovieMediaItems
            .Where(m => movies.Select(x => x.Id).Contains(m.MovieId) && !m.IsTrailer)
            .ToDictionaryAsync(m => m.MovieId, m => m.MediaItemId, ct);
        var start = PrimetimeSeed.LocalUtc(2026, 11, 3, 20, 15);
        var persisted = new List<ProgramSlot>();
        for (var i = 0; i < 3; i++)
        {
            var end = start + (i == 2 ? TimeSpan.FromMinutes(97) + TimeSpan.FromSeconds(5.352562)
                : TimeSpan.FromMinutes(71) + TimeSpan.FromSeconds(32.371156 + (i == 1 ? 228.043 : 0)));
            persisted.Add(new ProgramSlot
            {
                TVChannelId = channel.Id,
                Start = start,
                End = end,
                MovieId = movies[i].Id,
                MediaItemId = itemIds[movies[i].Id],
                ProgramRuleId = rule.Id,
                RuleType = rule.RuleType,
                Status = ProgramSlotStatus.Scheduled,
                CreatedAt = DateTime.UtcNow
            });
            start = end;
        }
        fixture.Db.ProgramSlots.AddRange(persisted);
        fixture.Db.ProgramRuleCursors.Add(new ProgramRuleCursor
        {
            ProgramRuleId = rule.Id,
            LastContentType = ProgramCursorContentType.Movie,
            LastContentId = movies[2].Id,
            UpdatedAt = DateTime.UtcNow
        });
        await fixture.Db.SaveChangesAsync(ct);

        // The coverage pass runs the morning of the airing day: coverageStart lands
        // exactly on the persisted chain end.
        var summary = await fixture.CreateService().EnsureCoverageAsync(
            new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 3, 4, 0)), ct);

        var all = await fixture.Db.ProgramSlots
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .OrderBy(s => s.Start).ToListAsync(ct);
        for (var i = 1; i < all.Count; i++)
            Assert.True(all[i].Start >= all[i - 1].End,
                $"Überschneidung: {all[i - 1].Id} ({all[i - 1].Start:HH:mm:ss}–{all[i - 1].End:HH:mm:ss}) " +
                $"vs. {all[i].Id} ({all[i].Start:HH:mm:ss}–{all[i].End:HH:mm:ss})");
    }
}
