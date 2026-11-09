using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Guide presentation of clip-group fill slots: a trailer clip carries <c>MovieId</c>, so the
/// guide entry shows the movie's details — same as trailer material slots do.
/// </summary>
public sealed class ProgramGuideServiceTests_ClipGroups
{
    [Fact]
    public async Task Guide_TrailerClipSlot_ShowsMovieDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = await ProgramScheduleTestDb.CreateAsync("guide-trailerclip", ct);
        var channel = await f.GetChannelAsync(ct);
        var movie = await f.CreateMovieAsync("Film mit Trailer", 1, TimeSpan.FromMinutes(90));
        var trailerItem = await f.CreateMediaItemAsync(1, TimeSpan.FromSeconds(60));
        var now = DateTime.UtcNow;
        await f.CreateSlotAsync(channel.Id, now, now + TimeSpan.FromSeconds(60), trailerItem.Id,
            movieId: movie.Id, ruleType: ProgramRuleType.MoviePrimeTime);

        var guide = f.CreateGuideService();
        var result = await guide.GetNowAsync(new DateTimeOffset(now + TimeSpan.FromSeconds(10)), ct);

        var entry = Assert.IsType<DtoProgramGuideEntry>(result.Now);
        Assert.Equal("Film mit Trailer", entry.Title);
    }

    /// <summary>
    /// Clip-fill slots carry their <c>IsClipFill</c> flag and producing <c>ProgramRuleId</c>
    /// into the guide entry — the Live-TV page needs both to collapse gapless same-rule clip
    /// runs into block rows (Issue #328).
    /// </summary>
    [Fact]
    public async Task Guide_ClipFillSlot_ExposesClipFlagAndRuleId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = await ProgramScheduleTestDb.CreateAsync("guide-clipfill-fields", ct);
        var channel = await f.GetChannelAsync(ct);
        var rule = await f.CreateRuleAsync(channel.Id, "Clipregel",
            ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily, new TimeOnly(20, 0),
            ProgramRuleMaterialType.Movie, materialId: 0);
        var clipItem = await f.CreateMediaItemAsync(1, TimeSpan.FromMinutes(5));
        var now = DateTime.UtcNow;
        await f.CreateSlotAsync(channel.Id, now, now + TimeSpan.FromMinutes(5), clipItem.Id,
            ruleId: rule.Id, isClipFill: true);
        await f.CreateSlotAsync(channel.Id, now + TimeSpan.FromMinutes(5), now + TimeSpan.FromMinutes(65), clipItem.Id,
            ruleId: rule.Id);

        var guide = f.CreateGuideService();
        var result = await guide.GetUpcomingAsync(new DateTimeOffset(now), TimeSpan.FromHours(2), ct);

        Assert.Equal(2, result.Length);
        Assert.True(result[0].IsClipFill);
        Assert.Equal(rule.Id, result[0].ProgramRuleId);
        Assert.False(result[1].IsClipFill);
        Assert.Equal(rule.Id, result[1].ProgramRuleId);
    }
}
