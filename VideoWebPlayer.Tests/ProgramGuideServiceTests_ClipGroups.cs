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
}
