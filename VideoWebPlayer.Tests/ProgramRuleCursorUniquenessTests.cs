using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Unique-index enforcement on <c>ProgramRuleCursors</c> (Issue #262): the filtered index
/// <c>IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey</c> allows at most one
/// NULL-<see cref="ProgramRuleCursor.StrandKey"/> row per rule, while the composite index
/// keeps covering the non-NULL strands. Runs against real SQLite via
/// <see cref="ProgramScheduleTestDb"/> — EF InMemory would not enforce either index.
/// </summary>
public sealed class ProgramRuleCursorUniquenessTests
{
    [Fact]
    public async Task ProgramRuleCursors_SecondNullStrandKeyRow_RejectedByUniqueIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cursor-unique-null", ct);
        var rule = await SeedRuleAsync(fixture);

        fixture.Db.ProgramRuleCursors.Add(Cursor(rule.Id, null));
        await fixture.Db.SaveChangesAsync(ct);

        fixture.Db.ProgramRuleCursors.Add(Cursor(rule.Id, null));
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task ProgramRuleCursors_DistinctStrandKeys_SameRule_Allowed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cursor-unique-strands", ct);
        var rule = await SeedRuleAsync(fixture);

        fixture.Db.ProgramRuleCursors.Add(Cursor(rule.Id, null));
        fixture.Db.ProgramRuleCursors.Add(Cursor(rule.Id, "show:1"));
        fixture.Db.ProgramRuleCursors.Add(Cursor(rule.Id, "other"));
        await fixture.Db.SaveChangesAsync(ct);

        Assert.Equal(3, await fixture.Db.ProgramRuleCursors.CountAsync(c => c.ProgramRuleId == rule.Id, ct));
    }

    [Fact]
    public async Task ProgramRuleCursors_SameNonNullStrandKey_SameRule_StillRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cursor-unique-composite", ct);
        var rule = await SeedRuleAsync(fixture);

        fixture.Db.ProgramRuleCursors.Add(Cursor(rule.Id, "show:1"));
        await fixture.Db.SaveChangesAsync(ct);

        fixture.Db.ProgramRuleCursors.Add(Cursor(rule.Id, "show:1"));
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync(ct));
    }

    private static ProgramRuleCursor Cursor(long ruleId, string? strandKey)
        => new()
        {
            ProgramRuleId = ruleId,
            StrandKey = strandKey,
            LastContentType = ProgramCursorContentType.TVShowEpisode,
            LastContentId = 1,
            UpdatedAt = DateTime.UtcNow
        };

    private static async Task<ProgramRule> SeedRuleAsync(ProgramScheduleTestDb fixture)
    {
        var channel = new TVChannel { Name = "Testsender" };
        fixture.Db.TVChannels.Add(channel);
        var rule = new ProgramRule
        {
            TVChannel = channel,
            Name = "Serienfenster",
            RuleType = ProgramRuleType.GenreRotation,
            Recurrence = ProgramRuleRecurrence.Daily,
            TimeOfDay = new TimeOnly(12, 0),
            EpisodeCount = 2,
            MaterialType = ProgramRuleMaterialType.Genre,
            MaterialId = 1,
            Priority = 5,
            IsActive = true
        };
        fixture.Db.ProgramRules.Add(rule);
        await fixture.SaveDiagAsync();
        return rule;
    }
}
