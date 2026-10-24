using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Deterministic cursor selection (<see cref="ProgramScheduleService.SelectStrandCursor"/>,
/// Issue #262): should several <see cref="ProgramRuleCursor"/> rows of one rule share a
/// <see cref="ProgramRuleCursor.StrandKey"/> — possible only before the filtered unique index
/// or through an unchecked path — the winner is always the youngest row (highest
/// <see cref="ProgramRuleCursor.UpdatedAt"/>, ties by highest <see cref="ProgramRuleCursor.Id"/>),
/// the same rule the data migration and the restore deduplication apply.
/// </summary>
public sealed class ProgramScheduleServiceTests_CursorSelection
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    private static readonly DateTimeOffset Now = new(LocalUtc(2026, 10, 24, 12, 0));

    [Fact]
    public void SelectStrandCursor_NullStrandKeyDuplicates_ReturnsYoungest()
    {
        var older = Cursor(id: 1, strandKey: null, lastContentId: 111, updatedAt: new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));
        var younger = Cursor(id: 2, strandKey: null, lastContentId: 222, updatedAt: new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));
        var show = Cursor(id: 3, strandKey: "show:9", lastContentId: 333, updatedAt: new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc));
        var other = Cursor(id: 4, strandKey: "other", lastContentId: 444, updatedAt: new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc));

        var winner = ProgramScheduleService.SelectStrandCursor(new[] { show, older, other, younger }, null);

        Assert.Same(younger, winner);
    }

    [Fact]
    public void SelectStrandCursor_TiedUpdatedAt_ReturnsHigherId()
    {
        var tied = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        var lowerId = Cursor(id: 5, strandKey: null, lastContentId: 111, updatedAt: tied);
        var higherId = Cursor(id: 9, strandKey: null, lastContentId: 222, updatedAt: tied);

        var winner = ProgramScheduleService.SelectStrandCursor(new[] { lowerId, higherId }, null);

        Assert.Same(higherId, winner);
    }

    [Fact]
    public async Task EnsureCoverage_NullStrandKeyDuplicates_PersistCursorsUpdatesWinner()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-cursor-dedup", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await fixture.CreateMediaItemAsync(1, TimeSpan.FromHours(1));
        await fixture.CreateMediaItemAsync(1, TimeSpan.FromHours(1));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Maskottchen", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.MediaSourceClips, 1, episodeCount: 1);

        // The filtered unique index forbids the duplicate on schema level — drop it on this
        // throwaway test database to forge the legacy inconsistency the read path must survive.
        await fixture.Db.Database.ExecuteSqlRawAsync("DROP INDEX [IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey]", ct);
        var loserUpdatedAt = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        // The loser is inserted first so it precedes the winner in the loaded row list —
        // a regression to an unordered FirstOrDefault would pick it instead of the winner.
        var loser = new ProgramRuleCursor
        {
            ProgramRuleId = rule.Id,
            StrandKey = null,
            LastContentType = ProgramCursorContentType.MediaItem,
            LastContentId = 424242,
            UpdatedAt = loserUpdatedAt
        };
        var winner = new ProgramRuleCursor
        {
            ProgramRuleId = rule.Id,
            StrandKey = null,
            LastContentType = ProgramCursorContentType.MediaItem,
            LastContentId = 1,
            UpdatedAt = loserUpdatedAt.AddDays(1)
        };
        fixture.Db.ProgramRuleCursors.AddRange(loser, winner);
        await fixture.Db.SaveChangesAsync(ct);
        var winnerId = winner.Id;
        var loserId = loser.Id;

        await fixture.CreateService().EnsureCoverageAsync(Now, ct);

        fixture.Db.ChangeTracker.Clear();
        var rows = await fixture.Db.ProgramRuleCursors.Where(c => c.ProgramRuleId == rule.Id).ToListAsync(ct);
        Assert.Equal(2, rows.Count);

        var slots = await fixture.SlotsAsync(ct);
        var updatedWinner = rows.Single(c => c.Id == winnerId);
        Assert.Equal(slots[^1].MediaItemId, updatedWinner.LastContentId);
        Assert.True(updatedWinner.UpdatedAt > loserUpdatedAt.AddDays(1));

        var untouchedLoser = rows.Single(c => c.Id == loserId);
        Assert.Equal(424242, untouchedLoser.LastContentId);
        Assert.Equal(loserUpdatedAt, untouchedLoser.UpdatedAt);
    }

    private static ProgramRuleCursor Cursor(long id, string? strandKey, long lastContentId, DateTime updatedAt)
        => new()
        {
            Id = id,
            ProgramRuleId = 7,
            StrandKey = strandKey,
            LastContentType = ProgramCursorContentType.TVShowEpisode,
            LastContentId = lastContentId,
            UpdatedAt = updatedAt
        };
}
