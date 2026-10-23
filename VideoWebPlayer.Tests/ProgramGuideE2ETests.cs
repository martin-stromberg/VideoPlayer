using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// HTTP-E2E tests for the program guide ("Programmauskunft") endpoints
/// <c>GET /api/program-guide/now</c> and <c>GET /api/program-guide/upcoming</c>: the secured
/// interface for signed-in users — anonymous requests are refused, a paired device reaches the
/// guide both via the Authorization header and via the <c>access_token</c> query parameter, and
/// the preview stays bounded while the full schedule remains administrative.
/// </summary>
[Trait("Category", "E2E")]
public sealed class ProgramGuideE2ETests : DeviceClientTestBase
{
    /// <summary>
    /// Seeds the delivered channel plus one running and one following broadcast slot so the guide
    /// has something to answer — the slots are written directly, no schedule generation needed.
    /// </summary>
    private async Task<(long RunningSlotId, long NextSlotId)> SeedChannelWithProgramAsync(string movieTitle)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var setup = await db.Setups.FirstOrDefaultAsync(TestContext.Current.CancellationToken) ?? db.Setups.Add(new Setup()).Entity;
        setup.TvSenderEnabled = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var channel = new TVChannel { Name = "E2E-Sender" };
        db.TVChannels.Add(channel);
        var source = new MediaSource { Name = $"E2E-Quelle {Guid.NewGuid():N}", Path = "/e2e", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        var collection = new MediaCollection { Name = $"E2E-Collection {Guid.NewGuid():N}", MediaSourceId = source.Id, Path = $"/{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync();

        var movie = new Movie { Name = movieTitle, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        var item = new MediaItem { Name = "e2e.mkv", Path = $"{collection.Path}/e2e.mkv", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(90), DurationProbedAt = DateTime.UtcNow };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync();
        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });

        var now = DateTime.UtcNow;
        var running = new ProgramSlot { TVChannelId = channel.Id, Start = now.AddMinutes(-30), End = now.AddMinutes(60), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.MoviePrimeTime, Status = ProgramSlotStatus.Scheduled };
        var following = new ProgramSlot { TVChannelId = channel.Id, Start = now.AddMinutes(60), End = now.AddMinutes(150), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.MoviePrimeTime, Status = ProgramSlotStatus.Scheduled };
        var displaced = new ProgramSlot { TVChannelId = channel.Id, Start = now.AddMinutes(150), End = now.AddMinutes(240), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.MoviePrimeTime, Status = ProgramSlotStatus.Displaced };
        db.ProgramSlots.AddRange(running, following, displaced);
        await db.SaveChangesAsync();
        return (running.Id, following.Id);
    }

    [Fact]
    public async Task Anonymous_RequestNow_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var http = CreateHttpClient();

        var response = await http.GetAsync("/api/program-guide/now", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_RequestUpcoming_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var http = CreateHttpClient();

        var response = await http.GetAsync("/api/program-guide/upcoming", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PairedDevice_RequestNow_ReturnsCurrentAndNext()
    {
        var ct = TestContext.Current.CancellationToken;
        var (runningSlotId, nextSlotId) = await SeedChannelWithProgramAsync("E2E-Abendfilm");
        var (_, payload) = await CreateUserAndPairDeviceAsync($"guide-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", payload.Token);
        var response = await http.GetAsync("/api/program-guide/now", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var guide = await response.Content.ReadFromJsonAsync<DtoProgramGuideNow>(ct);
        Assert.NotNull(guide);
        var now = Assert.IsType<DtoProgramGuideEntry>(guide.Now);
        Assert.Equal(runningSlotId, now.SlotId);
        Assert.Equal("E2E-Abendfilm", now.Title);
        Assert.True(now.PositionSeconds > 0);
        Assert.True(now.RemainingSeconds > 0);
        var next = Assert.IsType<DtoProgramGuideEntry>(guide.Next);
        Assert.Equal(nextSlotId, next.SlotId);
        Assert.Null(next.PositionSeconds);
    }

    [Fact]
    public async Task PairedDevice_RequestNowWithAccessTokenQuery_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedChannelWithProgramAsync("E2E-Query-Film");
        var (_, payload) = await CreateUserAndPairDeviceAsync($"guide-query-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/program-guide/now?access_token={payload.Token}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var guide = await response.Content.ReadFromJsonAsync<DtoProgramGuideNow>(ct);
        Assert.NotNull(guide?.Now);
    }

    [Fact]
    public async Task PairedDevice_RequestUpcoming_ReturnsBoundedEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (runningSlotId, nextSlotId) = await SeedChannelWithProgramAsync("E2E-Vorschau-Film");
        var (_, payload) = await CreateUserAndPairDeviceAsync($"guide-upcoming-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", payload.Token);

        var response = await http.GetAsync("/api/program-guide/upcoming?hours=3", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<DtoProgramGuideEntry[]>(ct);
        Assert.NotNull(entries);
        // Running + following within the window; the displaced slot never appears.
        Assert.Equal([runningSlotId, nextSlotId], entries.Select(e => e.SlotId).ToArray());
    }

    [Fact]
    public async Task PairedDevice_RequestUpcomingBeyondLimit_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedChannelWithProgramAsync("E2E-Limit-Film");
        var (_, payload) = await CreateUserAndPairDeviceAsync($"guide-limit-{Guid.NewGuid():N}@test.com", grantsTvAccess: true);

        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", payload.Token);

        var response = await http.GetAsync("/api/program-guide/upcoming?hours=99", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
