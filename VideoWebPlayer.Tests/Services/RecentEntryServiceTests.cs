using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public class RecentEntryServiceTests
{
    [Fact]
    public async Task GetRecentEntriesAsync_UserWithoutSourceAccess_SeesOnlyUnlockedEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:recententry-tests?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser { Id = userId, UserName = "regular@test.com" };
        var fakeAuth = new FakeAuthService { CurrentUser = user };

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();
        services.AddScoped<RecentEntryService>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);

        var source = new MediaSource
        {
            Name = "Test Source",
            Path = "/test",
            Host = "localhost",
            Port = 22
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var unlockedShow = new TVShow { Name = "Unlocked Show", MediaSourceId = source.Id };
        var otherShow = new TVShow { Name = "Other Show", MediaSourceId = source.Id };
        db.TVShows.Add(unlockedShow);
        db.TVShows.Add(otherShow);

        var unlockedCollection = new MovieCollection { Name = "Unlocked Collection", MediaSourceId = source.Id };
        var otherCollection = new MovieCollection { Name = "Other Collection", MediaSourceId = source.Id };
        db.MovieCollections.Add(unlockedCollection);
        db.MovieCollections.Add(otherCollection);
        await db.SaveChangesAsync(ct);

        var unlockedSeason = new TVShowSeason { Name = "Staffel 1", TVShowId = unlockedShow.Id };
        db.TVShowSeasons.Add(unlockedSeason);
        await db.SaveChangesAsync(ct);

        var unlockedEpisode = new TVShowEpisode
        {
            Name = "Episode 1",
            Number = 1,
            TVShowSeasonId = unlockedSeason.Id
        };
        db.TVShowEpisodes.Add(unlockedEpisode);
        await db.SaveChangesAsync(ct);

        db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = userId, TVShowId = unlockedShow.Id });
        db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = userId, MovieCollectionId = unlockedCollection.Id });
        await db.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;
        db.RecentEntries.AddRange(
            new RecentEntry { MediaSourceId = source.Id, TVShowId = unlockedShow.Id, CreatedAt = now.AddMinutes(-1) },
            new RecentEntry { MediaSourceId = source.Id, TVShowId = otherShow.Id, CreatedAt = now.AddMinutes(-2) },
            new RecentEntry { MediaSourceId = source.Id, TVShowSeasonId = unlockedSeason.Id, CreatedAt = now.AddMinutes(-3) },
            new RecentEntry { MediaSourceId = source.Id, TVShowEpisodeId = unlockedEpisode.Id, CreatedAt = now.AddMinutes(-4) },
            new RecentEntry { MediaSourceId = source.Id, MovieCollectionId = unlockedCollection.Id, CreatedAt = now.AddMinutes(-5) },
            new RecentEntry { MediaSourceId = source.Id, MovieCollectionId = otherCollection.Id, CreatedAt = now.AddMinutes(-6) },
            new RecentEntry { MediaSourceId = source.Id, CreatedAt = now.AddMinutes(-7) });

        await db.SaveChangesAsync(ct);

        var recentService = scope.ServiceProvider.GetRequiredService<RecentEntryService>();
        var entries = await recentService.GetRecentEntriesAsync();

        Assert.Equal(4, entries.Count);
        Assert.Contains(entries, e => e.TVShowId == unlockedShow.Id);
        Assert.Contains(entries, e => e.TVShowSeasonId == unlockedSeason.Id);
        Assert.Contains(entries, e => e.TVShowEpisodeId == unlockedEpisode.Id);
        Assert.Contains(entries, e => e.MovieCollectionId == unlockedCollection.Id);
        Assert.DoesNotContain(entries, e => e.TVShowId == otherShow.Id);
        Assert.DoesNotContain(entries, e => e.MovieCollectionId == otherCollection.Id);
        Assert.DoesNotContain(entries, e => e.TVShowId == null && e.MovieCollectionId == null && e.TVShowSeasonId == null && e.TVShowEpisodeId == null && e.MovieId == null);
    }

    private sealed class FakeAuthService : IAuthService
    {
        public ApplicationUser? CurrentUser { get; set; }

        public Task<AuthorizationToken> ImpersonateAsync(ImpersonateRequest request)
            => throw new NotImplementedException();

        public Task<AuthorizationToken> LoginAsync(AuthenticationRequest request)
            => throw new NotImplementedException();
    }
}
