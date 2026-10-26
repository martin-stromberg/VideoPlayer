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
        using var ctx = await CreateContextAsync("source-visibility-tests", ct);
        var db = ctx.Db;
        var userId = ctx.User.Id;

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

        var sourcesController = new SourcesController(ctx.Auth, db, ctx.UnlockedMedia, NullLogger<SourcesController>.Instance);
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
        using var ctx = await CreateContextAsync("source-items-visibility-tests", ct);
        var db = ctx.Db;
        var userId = ctx.User.Id;

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

        var recentEntryService = new RecentEntryService(db, ctx.Auth, ctx.UnlockedMedia);
        var itemsController = new ItemsController(
            db,
            new SftpMediaSourceReader(),
            new MediaMetadataEditorService(db, null),
            recentEntryService,
            ctx.UnlockedMedia,
            ctx.Auth,
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

    [Fact]
    public async Task GetSources_Returns_HiddenInMenu_Flag_And_Keeps_Hidden_Source_In_List()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await CreateContextAsync("source-hiddeninmenu-list-tests", ct);
        var (visibleSource, hiddenSource) = await SeedVisibleAndHiddenSourceAsync(ctx, ct);

        var sourcesController = new SourcesController(ctx.Auth, ctx.Db, ctx.UnlockedMedia, NullLogger<SourcesController>.Instance);
        var listResult = await sourcesController.GetSources();

        var okResult = Assert.IsType<OkObjectResult>(listResult);
        var sources = Assert.IsAssignableFrom<IEnumerable<DtoMediaSource>>(okResult.Value).ToList();

        // Die Filterung des Menüs ist UI-Aufgabe — die API-Liste bleibt vollständig.
        var visibleDto = Assert.Single(sources, s => s.Id == visibleSource.Id);
        var hiddenDto = Assert.Single(sources, s => s.Id == hiddenSource.Id);
        Assert.False(visibleDto.HiddenInMenu);
        Assert.True(hiddenDto.HiddenInMenu);
    }

    [Fact]
    public async Task GetSource_Returns_HiddenInMenu_Flag()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await CreateContextAsync("source-hiddeninmenu-single-tests", ct);
        var (_, hiddenSource) = await SeedVisibleAndHiddenSourceAsync(ctx, ct);

        var sourcesController = new SourcesController(ctx.Auth, ctx.Db, ctx.UnlockedMedia, NullLogger<SourcesController>.Instance);
        var singleResult = await sourcesController.GetSource(hiddenSource.Id);

        var singleOk = Assert.IsType<OkObjectResult>(singleResult);
        var singleDto = Assert.IsType<DtoMediaSource>(singleOk.Value);
        Assert.True(singleDto.HiddenInMenu);
    }

    /// <summary>
    /// Shared-Cache-InMemory-SQLite samt DI-Container (EventManager, ApplicationDbContext, IAuthService,
    /// IUnlockedMediaService) und einem angemeldeten Regular-User — bisher dreimal identisch wiederholt.
    /// Der Rückgabewert disposed Scope, ServiceProvider und Keeper-Connection.
    /// </summary>
    private static async Task<ControllerTestContext> CreateContextAsync(string databaseName, CancellationToken ct)
    {
        var connectionString = $"Data Source=file:{databaseName}?mode=memory&cache=shared";
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "regular@test.com" };
        var fakeAuth = new FakeAuthService { CurrentUser = user };

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();
        var serviceProvider = services.BuildServiceProvider();

        var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return new ControllerTestContext(
            keeperConnection,
            serviceProvider,
            scope,
            db,
            user,
            fakeAuth,
            scope.ServiceProvider.GetRequiredService<IUnlockedMediaService>());
    }

    private static async Task<(MediaSource Visible, MediaSource Hidden)> SeedVisibleAndHiddenSourceAsync(
        ControllerTestContext ctx, CancellationToken ct)
    {
        var visibleSource = new MediaSource { Name = "Visible Source", Path = "/a", Host = "localhost", Port = 22 };
        var hiddenSource = new MediaSource { Name = "Hidden Source", Path = "/b", Host = "localhost", Port = 22, HiddenInMenu = true };
        ctx.Db.MediaSources.AddRange(visibleSource, hiddenSource);
        await ctx.Db.SaveChangesAsync(ct);

        ctx.Db.MediaSourceUsers.AddRange(
            new MediaSourceUser { UserId = ctx.User.Id, MediaSourceId = visibleSource.Id },
            new MediaSourceUser { UserId = ctx.User.Id, MediaSourceId = hiddenSource.Id });
        await ctx.Db.SaveChangesAsync(ct);

        return (visibleSource, hiddenSource);
    }

    private sealed class ControllerTestContext(
        SqliteConnection keeperConnection,
        ServiceProvider serviceProvider,
        IServiceScope scope,
        ApplicationDbContext db,
        ApplicationUser user,
        FakeAuthService auth,
        IUnlockedMediaService unlockedMedia) : IDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public ApplicationUser User { get; } = user;
        public FakeAuthService Auth { get; } = auth;
        public IUnlockedMediaService UnlockedMedia { get; } = unlockedMedia;

        public void Dispose()
        {
            scope.Dispose();
            serviceProvider.Dispose();
            keeperConnection.Dispose();
        }
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
