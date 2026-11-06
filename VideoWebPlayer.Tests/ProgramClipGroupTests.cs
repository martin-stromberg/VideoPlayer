using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// SQLite persistence constraints of the clip-group tables: composite keys, NOCASE-unique
/// group names and cascade deletes.
/// </summary>
public sealed class ProgramClipGroupTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GroupName_IsUnique_NoCase()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cg-uniquename", ct);
        await f.CreateClipGroupAsync("Maskottchen", ct);
        f.Db.ProgramClipGroups.Add(new ProgramClipGroup { Name = "maskottchen" });
        await Assert.ThrowsAnyAsync<Exception>(async () => await f.Db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task DuplicateMember_CompositeKeyRejected()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cg-dupmember", ct);
        var g = await f.CreateClipGroupAsync("G", ct);
        var item = await f.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        await f.AddClipToGroupAsync(g.Id, item.Id, ct);
        // Real SQLite composite-PK enforcement (bypasses the EF change tracker).
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await f.Db.Database.ExecuteSqlRawAsync(
                "INSERT INTO ProgramClipGroupItems (ProgramClipGroupId, MediaItemId) VALUES ({0}, {1})", g.Id, item.Id));
    }

    [Fact]
    public async Task DeleteGroup_CascadesMembersAndRuleLinks()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cg-cascade", ct);
        var channel = await f.GetChannelAsync(ct);
        var g = await f.CreateClipGroupAsync("G", ct);
        var item = await f.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        await f.AddClipToGroupAsync(g.Id, item.Id, ct);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var rule = await f.CreateRuleAsync(channel.Id, "R", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(2), clipGroupIds: [g.Id]);

        f.Db.ProgramClipGroups.Remove(g);
        await f.Db.SaveChangesAsync(ct);

        Assert.False(await f.Db.ProgramClipGroupItems.AnyAsync(ct));
        Assert.False(await f.Db.ProgramRuleClipGroups.AnyAsync(ct));
        Assert.NotNull(await f.Db.ProgramRules.FindAsync([rule.Id], ct));
        Assert.NotNull(await f.Db.MediaItems.FindAsync([item.Id], ct));
    }

    [Fact]
    public async Task DeleteRule_CascadesClipGroupLinks()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cg-rulecascade", ct);
        var channel = await f.GetChannelAsync(ct);
        var g = await f.CreateClipGroupAsync("G", ct);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var rule = await f.CreateRuleAsync(channel.Id, "R", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(2), clipGroupIds: [g.Id]);

        f.Db.ProgramRules.Remove(rule);
        await f.Db.SaveChangesAsync(ct);

        Assert.False(await f.Db.ProgramRuleClipGroups.AnyAsync(ct));
        Assert.NotNull(await f.Db.ProgramClipGroups.FindAsync([g.Id], ct));
    }

    [Fact]
    public async Task DeleteMediaItem_CascadesMembership()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cg-itemcascade", ct);
        var g = await f.CreateClipGroupAsync("G", ct);
        var item = await f.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        await f.AddClipToGroupAsync(g.Id, item.Id, ct);

        f.Db.MediaItems.Remove(item);
        await f.Db.SaveChangesAsync(ct);

        Assert.False(await f.Db.ProgramClipGroupItems.AnyAsync(ct));
    }
}
