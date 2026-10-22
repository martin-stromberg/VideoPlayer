using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="ProgramGuideService"/> — the purely time-based program guide
/// ("Programmauskunft") over the program schedule. All runs go against real in-memory SQLite via
/// <see cref="ProgramScheduleTestDb"/>; slots are seeded directly, no schedule generation needed.
/// </summary>
public sealed class ProgramGuideServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 20, 45, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetNowAsync_RunningSlot_ReturnsEntryWithPositionAndRemaining()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-now-running", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Abendfilm", duration: TimeSpan.FromMinutes(90));
        var nextMovie = await fixture.CreateMovieAsync("Spaetfilm", duration: TimeSpan.FromMinutes(60));
        var item = await fixture.Db.MovieMediaItems.Where(m => m.MovieId == movie.Id).Select(m => m.MediaItemId).SingleAsync(ct);
        var nextItem = await fixture.Db.MovieMediaItems.Where(m => m.MovieId == nextMovie.Id).Select(m => m.MediaItemId).SingleAsync(ct);

        var running = await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(-45), Now.AddMinutes(45), item, movieId: movie.Id);
        var following = await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(45), Now.AddMinutes(105), nextItem, movieId: nextMovie.Id);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        Assert.Equal(new DateTimeOffset(Now), result.ServerTimeUtc);
        var now = Assert.IsType<DtoProgramGuideEntry>(result.Now);
        Assert.Equal(running.Id, now.SlotId);
        Assert.Equal("Abendfilm", now.Title);
        Assert.Equal(MediaTypeValues.Movie, now.MediaType);
        Assert.Equal(movie.Id, now.MediaId);
        Assert.Equal(item, now.MediaItemId);
        Assert.Equal(45 * 60, now.PositionSeconds);
        Assert.Equal(45 * 60, now.RemainingSeconds);
        Assert.Equal(90 * 60, now.DurationSeconds);
        Assert.False(now.IsFiller);

        var next = Assert.IsType<DtoProgramGuideEntry>(result.Next);
        Assert.Equal(following.Id, next.SlotId);
        Assert.Equal("Spaetfilm", next.Title);
        Assert.Null(next.PositionSeconds);
        Assert.Null(next.RemainingSeconds);
    }

    [Fact]
    public async Task GetNowAsync_DisplacedSlotCoveringNow_IsNotReported()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-now-displaced", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));

        await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(-45), Now.AddMinutes(45), item.Id,
            status: ProgramSlotStatus.Displaced);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        Assert.Null(result.Now);
        Assert.Null(result.Next);
    }

    [Fact]
    public async Task GetNowAsync_ScheduleGap_ReturnsNullNowButNamesNext()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-now-gap", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Spaetstart", duration: TimeSpan.FromMinutes(60));
        var item = await fixture.Db.MovieMediaItems.Where(m => m.MovieId == movie.Id).Select(m => m.MediaItemId).SingleAsync(ct);

        // A gap until the next broadcast in 15 minutes.
        var following = await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(15), Now.AddMinutes(75), item, movieId: movie.Id);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        Assert.Null(result.Now);
        var next = Assert.IsType<DtoProgramGuideEntry>(result.Next);
        Assert.Equal(following.Id, next.SlotId);
        Assert.Equal("Spaetstart", next.Title);
    }

    [Fact]
    public async Task GetNowAsync_ExactlyAtSlotEnd_NextSlotIsCurrent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-now-boundary", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var first = await fixture.CreateMovieAsync("Erster", duration: TimeSpan.FromMinutes(90));
        var second = await fixture.CreateMovieAsync("Zweiter", duration: TimeSpan.FromMinutes(60));
        var firstItem = await fixture.Db.MovieMediaItems.Where(m => m.MovieId == first.Id).Select(m => m.MediaItemId).SingleAsync(ct);
        var secondItem = await fixture.Db.MovieMediaItems.Where(m => m.MovieId == second.Id).Select(m => m.MediaItemId).SingleAsync(ct);

        await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(-90), Now, firstItem, movieId: first.Id);
        var secondSlot = await fixture.CreateSlotAsync(channel.Id, Now, Now.AddMinutes(60), secondItem, movieId: second.Id);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        var now = Assert.IsType<DtoProgramGuideEntry>(result.Now);
        Assert.Equal(secondSlot.Id, now.SlotId);
        Assert.Equal(0, now.PositionSeconds);
        // The old slot must not resurface as "next" either.
        Assert.Null(result.Next);
    }

    [Fact]
    public async Task GetNowAsync_EpisodeSlot_ReturnsFormattedEpisodeTitle()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-now-episode", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 2, duration: TimeSpan.FromMinutes(45));
        var episode = episodes[0];
        var item = await fixture.Db.TVShowEpisodeMediaItems.Where(m => m.TVShowEpisodeId == episode.Id).Select(m => m.MediaItemId).SingleAsync(ct);

        var slot = await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(-10), Now.AddMinutes(35), item,
            episodeId: episode.Id, ruleType: ProgramRuleType.SeriesFixedTime);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        var now = Assert.IsType<DtoProgramGuideEntry>(result.Now);
        Assert.Equal(slot.Id, now.SlotId);
        Assert.Equal(MediaTypeValues.TVShowEpisode, now.MediaType);
        Assert.Equal(episode.Id, now.MediaId);
        Assert.Equal($"Serie — Staffel 1, E{episode.Number}: {episode.Name}", now.Title);
        Assert.False(now.IsFiller);
    }

    [Fact]
    public async Task GetNowAsync_FillerSlot_IsMarkedAsFiller()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-now-filler", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(30));

        await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(-10), Now.AddMinutes(20), item.Id,
            ruleType: ProgramRuleType.Filler);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        var now = Assert.IsType<DtoProgramGuideEntry>(result.Now);
        Assert.True(now.IsFiller);
    }

    [Fact]
    public async Task GetNowAsync_DeletedMovie_FallsBackToUnknownTitle()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-now-deleted", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));

        // Soft reference pointing at a movie that no longer exists.
        await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(-45), Now.AddMinutes(45), item.Id, movieId: 424242);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        var now = Assert.IsType<DtoProgramGuideEntry>(result.Now);
        Assert.Equal("Unbekannt", now.Title);
        Assert.Equal(MediaTypeValues.Movie, now.MediaType);
        Assert.Equal(424242, now.MediaId);
    }

    [Fact]
    public async Task GetNowAsync_NoChannel_ReturnsEmptyNowAndNext()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-now-nochannel", ct);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        Assert.Equal(new DateTimeOffset(Now), result.ServerTimeUtc);
        Assert.Null(result.Now);
        Assert.Null(result.Next);
        // And it must not have created the channel as a side effect.
        Assert.False(await fixture.Db.TVChannels.AnyAsync(ct));
    }

    [Fact]
    public async Task GetUpcomingAsync_ReturnsRunningAndFollowingWithinWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-upcoming-window", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movieA = await fixture.CreateMovieAsync("Film A", duration: TimeSpan.FromMinutes(90));
        var movieB = await fixture.CreateMovieAsync("Film B", duration: TimeSpan.FromMinutes(90));
        var movieC = await fixture.CreateMovieAsync("Film C", duration: TimeSpan.FromMinutes(90));
        var itemA = await fixture.Db.MovieMediaItems.Where(m => m.MovieId == movieA.Id).Select(m => m.MediaItemId).SingleAsync(ct);
        var itemB = await fixture.Db.MovieMediaItems.Where(m => m.MovieId == movieB.Id).Select(m => m.MediaItemId).SingleAsync(ct);
        var itemC = await fixture.Db.MovieMediaItems.Where(m => m.MovieId == movieC.Id).Select(m => m.MediaItemId).SingleAsync(ct);

        var running = await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(-45), Now.AddMinutes(45), itemA, movieId: movieA.Id);
        var second = await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(45), Now.AddMinutes(135), itemB, movieId: movieB.Id);
        // Starts exactly at the 2-hour window edge — excluded (half-open window).
        await fixture.CreateSlotAsync(channel.Id, Now.AddHours(2), Now.AddHours(3).AddMinutes(30), itemC, movieId: movieC.Id);

        var result = await fixture.CreateGuideService().GetUpcomingAsync(new DateTimeOffset(Now), TimeSpan.FromHours(2), ct);

        Assert.Equal([running.Id, second.Id], result.Select(e => e.SlotId).ToArray());
        Assert.Equal("Film A", result[0].Title);
        Assert.Equal("Film B", result[1].Title);
        Assert.Equal(45 * 60, result[0].PositionSeconds);
        Assert.Equal(45 * 60, result[0].RemainingSeconds);
        Assert.Null(result[1].PositionSeconds);
        Assert.Null(result[1].RemainingSeconds);
    }

    [Fact]
    public async Task GetUpcomingAsync_ExcludesDisplacedSlots()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-upcoming-displaced", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(60));

        await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(30), Now.AddMinutes(90), item.Id,
            status: ProgramSlotStatus.Displaced);
        var kept = await fixture.CreateSlotAsync(channel.Id, Now.AddMinutes(90), Now.AddMinutes(150), item.Id);

        var result = await fixture.CreateGuideService().GetUpcomingAsync(new DateTimeOffset(Now), TimeSpan.FromHours(6), ct);

        Assert.Equal([kept.Id], result.Select(e => e.SlotId).ToArray());
    }

    [Fact]
    public async Task GetUpcomingAsync_InvalidPreview_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-upcoming-invalid", ct);
        var service = fixture.CreateGuideService();
        var now = new DateTimeOffset(Now);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetUpcomingAsync(now, TimeSpan.Zero, ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetUpcomingAsync(now, TimeSpan.FromHours(-1), ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetUpcomingAsync(now, TimeSpan.FromHours(ProgramGuideService.MaxPreviewHours).Add(TimeSpan.FromTicks(1)), ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetUpcomingAsync(now, TimeSpan.FromHours(48), ct));
    }

    [Fact]
    public async Task GetUpcomingAsync_NoChannel_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-upcoming-nochannel", ct);

        var result = await fixture.CreateGuideService().GetUpcomingAsync(new DateTimeOffset(Now), TimeSpan.FromHours(6), ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNowAsync_BareMediaItemSlot_FallsBackToItemName()
    {
        // Clip-source filler slots carry only MediaItemId — the guide shows the file's name
        // instead of "Unbekannt" (mascot clips have no movie/episode entry by design).
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("guide-clip-title", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var clip = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromSeconds(15));
        clip.Name = "funki-begruessung.mp4";
        await fixture.SaveDiagAsync();

        await fixture.CreateSlotAsync(channel.Id, Now.AddSeconds(-10), Now.AddSeconds(5), clip.Id,
            ruleType: ProgramRuleType.Filler);

        var result = await fixture.CreateGuideService().GetNowAsync(new DateTimeOffset(Now), ct);

        var now = Assert.IsType<DtoProgramGuideEntry>(result.Now);
        Assert.Equal("funki-begruessung.mp4", now.Title);
        Assert.True(now.IsFiller);
        Assert.Null(now.MediaType);
        Assert.Null(now.MediaId);
        Assert.Equal(clip.Id, now.MediaItemId);
    }
}
