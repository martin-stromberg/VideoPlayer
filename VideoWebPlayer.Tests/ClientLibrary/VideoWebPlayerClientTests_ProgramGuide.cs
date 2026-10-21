using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// <see cref="IProgramGuideApiClient"/>-Methoden des <see cref="global::VideoWebPlayer.Client.VideoWebPlayerClient"/>
/// gegen die gehostete Anwendung: ein gekoppeltes Gerät liest Jetzt/Gleich und die begrenzte Vorschau.
/// </summary>
public sealed class VideoWebPlayerClientTests_ProgramGuide : DeviceClientTestBase
{
    private async Task<(long RunningSlotId, long NextSlotId)> SeedChannelWithProgramAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var channel = new TVChannel { Name = "Client-Guide-Sender" };
        db.TVChannels.Add(channel);
        var source = new MediaSource { Name = $"Guide-Quelle {Guid.NewGuid():N}", Path = "/guide", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        var collection = new MediaCollection { Name = $"Guide-Collection {Guid.NewGuid():N}", MediaSourceId = source.Id, Path = $"/{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync();

        var movie = new Movie { Name = "Client-Guide-Film", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        var item = new MediaItem { Name = "guide.mkv", Path = $"{collection.Path}/guide.mkv", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(90), DurationProbedAt = DateTime.UtcNow };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync();
        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });

        var now = DateTime.UtcNow;
        var running = new ProgramSlot { TVChannelId = channel.Id, Start = now.AddMinutes(-30), End = now.AddMinutes(60), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.MoviePrimeTime, Status = ProgramSlotStatus.Scheduled };
        var following = new ProgramSlot { TVChannelId = channel.Id, Start = now.AddMinutes(60), End = now.AddMinutes(150), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.MoviePrimeTime, Status = ProgramSlotStatus.Scheduled };
        db.ProgramSlots.AddRange(running, following);
        await db.SaveChangesAsync();
        return (running.Id, following.Id);
    }

    [Fact]
    public async Task PairedDevice_RequestProgramGuideNow_ReturnsCurrentAndNext()
    {
        var ct = TestContext.Current.CancellationToken;
        var (runningSlotId, nextSlotId) = await SeedChannelWithProgramAsync();
        await CreateUserAndPairDeviceAsync($"guide-client-{Guid.NewGuid():N}@test.com");

        var guide = await Client.RequestProgramGuideNowAsync(ct);

        var now = Assert.IsType<DtoProgramGuideEntry>(guide.Now);
        Assert.Equal(runningSlotId, now.SlotId);
        Assert.Equal("Client-Guide-Film", now.Title);
        Assert.True(now.PositionSeconds > 0);
        var next = Assert.IsType<DtoProgramGuideEntry>(guide.Next);
        Assert.Equal(nextSlotId, next.SlotId);
    }

    [Fact]
    public async Task PairedDevice_RequestProgramGuideUpcoming_ReturnsBoundedEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (runningSlotId, nextSlotId) = await SeedChannelWithProgramAsync();
        await CreateUserAndPairDeviceAsync($"guide-client-up-{Guid.NewGuid():N}@test.com");

        var entries = await Client.RequestProgramGuideUpcomingAsync(3, ct);

        Assert.Equal([runningSlotId, nextSlotId], entries.Select(e => e.SlotId).ToArray());
    }
}
