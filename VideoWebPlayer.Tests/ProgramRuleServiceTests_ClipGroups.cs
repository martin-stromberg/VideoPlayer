using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Validation and persistence of the clip-group assignment on program rules: filler rules and
/// rules without a slot duration reject groups, unknown groups are rejected, the reserved
/// "Trailer" group is assignable, the grace range is validated and links round-trip.
/// </summary>
public sealed class ProgramRuleServiceTests_ClipGroups
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static ProgramRuleInput Input(long materialId, ProgramRuleType type = ProgramRuleType.MoviePrimeTime,
        TimeSpan? slotDuration = null, IReadOnlyList<long>? clipGroupIds = null, TimeSpan? grace = null,
        int? episodeCount = 1)
        => new("R", type, ProgramRuleRecurrence.Daily, null, null, new TimeOnly(20, 0),
            episodeCount, slotDuration, ProgramRuleMaterialType.Movie, materialId, 0, true,
            null, null, clipGroupIds, grace);

    [Fact]
    public async Task FillerRule_RejectsClipGroups()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cgr-filler", ct);
        var channel = await f.GetChannelAsync(ct);
        var admin = await f.AddUserAsync("a@t.t", isAdmin: true);
        var g = await f.CreateClipGroupAsync("G", ct);
        var svc = new ProgramRuleService(f.Db);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(1, ProgramRuleType.Filler, slotDuration: TimeSpan.FromHours(1), clipGroupIds: [g.Id]), ct));
    }

    [Fact]
    public async Task NoSlotDuration_RejectsClipGroups()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cgr-noslot", ct);
        var channel = await f.GetChannelAsync(ct);
        var admin = await f.AddUserAsync("a@t.t", isAdmin: true);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var g = await f.CreateClipGroupAsync("G", ct);
        var svc = new ProgramRuleService(f.Db);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(movie.Id, clipGroupIds: [g.Id]), ct));
    }

    [Fact]
    public async Task NoSlotDuration_RejectsGrace()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cgr-grace-noslot", ct);
        var channel = await f.GetChannelAsync(ct);
        var admin = await f.AddUserAsync("a@t.t", isAdmin: true);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var svc = new ProgramRuleService(f.Db);
        // Parallel zu ClipGroupIds: die Karenzzeit wirkt nur mit Sendeplatz-Länge — ohne
        // Länge wird sie abgelehnt statt als wirkungsloser Wert persistiert zu werden.
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(movie.Id, grace: TimeSpan.FromMinutes(5)), ct));
        var rule = await svc.CreateRuleAsync(admin.Id, channel.Id, Input(movie.Id), ct);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.UpdateRuleAsync(admin.Id, rule.Id,
            Input(movie.Id, grace: TimeSpan.FromMinutes(5)), ct));
    }

    [Fact]
    public async Task UnknownGroup_Rejected()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cgr-unknown", ct);
        var channel = await f.GetChannelAsync(ct);
        var admin = await f.AddUserAsync("a@t.t", isAdmin: true);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var svc = new ProgramRuleService(f.Db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(movie.Id, slotDuration: TimeSpan.FromHours(1), clipGroupIds: [999]), ct));
    }

    [Fact]
    public async Task GraceRange_Validated()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cgr-grace", ct);
        var channel = await f.GetChannelAsync(ct);
        var admin = await f.AddUserAsync("a@t.t", isAdmin: true);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var g = await f.CreateClipGroupAsync("G", ct);
        var svc = new ProgramRuleService(f.Db);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(movie.Id, slotDuration: TimeSpan.FromHours(1), clipGroupIds: [g.Id], grace: TimeSpan.Zero), ct));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(movie.Id, slotDuration: TimeSpan.FromHours(1), clipGroupIds: [g.Id], grace: TimeSpan.FromHours(1)), ct));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(movie.Id, slotDuration: TimeSpan.FromHours(1), clipGroupIds: [g.Id], grace: TimeSpan.FromHours(2)), ct));
    }

    [Fact]
    public async Task ClipGroups_LinkRoundtrip_AndTrailerAllowed()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cgr-roundtrip", ct);
        var channel = await f.GetChannelAsync(ct);
        var admin = await f.AddUserAsync("a@t.t", isAdmin: true);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var g = await f.CreateClipGroupAsync("G", ct);
        var svc = new ProgramRuleService(f.Db);

        var rule = await svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(movie.Id, slotDuration: TimeSpan.FromHours(4), clipGroupIds: [g.Id, ProgramClipGroup.TrailerGroupId], grace: TimeSpan.FromMinutes(10)), ct);
        Assert.Equal(TimeSpan.FromMinutes(10), rule.PlaceholderGrace);

        var loaded = (await svc.GetRulesAsync(admin.Id, channel.Id, ct)).Single(r => r.Id == rule.Id);
        Assert.Equal(new[] { ProgramClipGroup.TrailerGroupId, g.Id }, loaded.ClipGroups.Select(c => c.ProgramClipGroupId).OrderBy(x => x).ToArray());
        Assert.Equal(TimeSpan.FromMinutes(10), loaded.PlaceholderGrace);

        await svc.UpdateRuleAsync(admin.Id, rule.Id,
            Input(movie.Id, slotDuration: TimeSpan.FromHours(4), clipGroupIds: [g.Id]), ct);
        var reloaded = (await svc.GetRulesAsync(admin.Id, channel.Id, ct)).Single(r => r.Id == rule.Id);
        Assert.Equal(new[] { g.Id }, reloaded.ClipGroups.Select(c => c.ProgramClipGroupId).ToArray());
        Assert.Equal(ProgramRule.DefaultPlaceholderGrace, reloaded.PlaceholderGrace);
    }

    [Fact]
    public async Task DefaultGrace_IsFifteenMinutes()
    {
        var ct = CT;
        using var f = await ProgramScheduleTestDb.CreateAsync("cgr-default", ct);
        var channel = await f.GetChannelAsync(ct);
        var admin = await f.AddUserAsync("a@t.t", isAdmin: true);
        var movie = await f.CreateMovieAsync("M", 1, TimeSpan.FromMinutes(90));
        var svc = new ProgramRuleService(f.Db);
        var rule = await svc.CreateRuleAsync(admin.Id, channel.Id,
            Input(movie.Id, slotDuration: TimeSpan.FromHours(4)), ct);
        Assert.Equal(TimeSpan.FromMinutes(15), rule.PlaceholderGrace);
    }
}
