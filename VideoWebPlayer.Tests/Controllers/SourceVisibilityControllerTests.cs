using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

public class SourceVisibilityControllerTests
{
    [Fact]
    public async Task GetSources_Includes_Sources_With_Unlocked_Items()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:source-visibility-tests?mode=memory&cache=shared";
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

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);

        var allowedSource = new MediaSource { Name = "Allowed Source", Path = "/a", Host = "localhost", Port = 22 };
        var unlockedOnlySource = new MediaSource { Name = "Unlocked Source", Path = "/b", Host = "localhost", Port = 22 };
        var noAccessSource = new MediaSource { Name = "No Access Source", Path = "/c", Host = "localhost", Port = 22 };
        db.MediaSources.AddRange(allowedSource, unlockedOnlySource, noAccessSource);
        await db.SaveChangesAsync(ct);

        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = userId, MediaSourceId = allowedSource.Id });

        var unlockedShow = new TVShow { Name = "Unlocked Show", MediaSourceId = unlockedOnlySource.Id };
        var otherShow = new TVShow { Name = "Other Show", MediaSourceId = noAccessSource.Id };
        db.TVShows.AddRange(unlockedShow, otherShow);
        await db.SaveChangesAsync(ct);

        db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = userId, TVShowId = unlockedShow.Id });
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = scope.ServiceProvider.GetRequiredService<IUnlockedMediaService>();
        var sourcesController = new SourcesController(fakeAuth, db, unlockedMediaService, NullLogger<SourcesController>.Instance);
        var result = await sourcesController.GetSources();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var sources = Assert.IsAssignableFrom<IEnumerable<DtoMediaSource>>(okResult.Value);
        var sourceIds = sources.Select(s => s.Id).ToList();

        Assert.Contains(allowedSource.Id, sourceIds);
        Assert.Contains(unlockedOnlySource.Id, sourceIds);
        Assert.DoesNotContain(noAccessSource.Id, sourceIds);
    }

    [Fact]
    public async Task Get_Items_For_Unlocked_Source_Lists_Only_Unlocked_Entries()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:source-items-visibility-tests?mode=memory&cache=shared";
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

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);

        var source = new MediaSource { Name = "Unlocked Source", Path = "/b", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var unlockedCollection = new MovieCollection { Name = "Unlocked Collection", MediaSourceId = source.Id };
        var otherCollection = new MovieCollection { Name = "Other Collection", MediaSourceId = source.Id };
        var unlockedShow = new TVShow { Name = "Unlocked Show", MediaSourceId = source.Id };
        var otherShow = new TVShow { Name = "Other Show", MediaSourceId = source.Id };
        db.MovieCollections.AddRange(unlockedCollection, otherCollection);
        db.TVShows.AddRange(unlockedShow, otherShow);
        await db.SaveChangesAsync(ct);

        db.UnlockedMediaEntries.AddRange(
            new UnlockedMediaEntry { UserId = userId, MovieCollectionId = unlockedCollection.Id },
            new UnlockedMediaEntry { UserId = userId, TVShowId = unlockedShow.Id });
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = scope.ServiceProvider.GetRequiredService<IUnlockedMediaService>();
        var recentEntryService = new RecentEntryService(db, fakeAuth, unlockedMediaService);
        var itemsController = new ItemsController(
            db,
            new SftpMediaSourceReader(),
            new MediaMetadataEditorService(db, null),
            recentEntryService,
            unlockedMediaService,
            fakeAuth,
            NullLogger<ItemsController>.Instance);

        var actionResult = await itemsController.Get(mediaSourceId: source.Id, page: 0, size: 100, search: null, genreId: null);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var items = Assert.IsType<List<MediaEntryDto>>(okResult.Value);
        var itemIds = items.Select(i => i.Id).ToList();

        Assert.Contains(unlockedCollection.Id, itemIds);
        Assert.Contains(unlockedShow.Id, itemIds);
        Assert.DoesNotContain(otherCollection.Id, itemIds);
        Assert.DoesNotContain(otherShow.Id, itemIds);
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
