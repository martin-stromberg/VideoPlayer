using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

public class ItemsControllerAccessTests
{
    [Fact]
    public async Task Get_MovieCollection_Without_Access_Returns_Unauthorized()
    {
        var (db, controller, source, collection, _, _) = await CreateControllerWithUnlockedCollectionAsync(false);

        var result = await controller.Get("moviecollection", collection.Id);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Get_MovieCollection_Unlocked_Returns_Ok()
    {
        var (db, controller, source, collection, _, _) = await CreateControllerWithUnlockedCollectionAsync(true);

        var result = await controller.Get("moviecollection", collection.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoMovieCollection>(okResult.Value);
        Assert.Equal(collection.Id, dto.Id);
    }

    [Fact]
    public async Task Get_TVShow_Without_Access_Returns_Unauthorized()
    {
        var (db, controller, source, show, _, _) = await CreateControllerWithUnlockedShowAsync(false);

        var result = await controller.Get("tvshow", show.Id);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Get_TVShow_Unlocked_Returns_Ok()
    {
        var (db, controller, source, show, _, _) = await CreateControllerWithUnlockedShowAsync(true);

        var result = await controller.Get("tvshow", show.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoTVShow>(okResult.Value);
        Assert.Equal(show.Id, dto.Id);
    }

    [Fact]
    public async Task Get_TVShowEpisode_Without_Access_Returns_Unauthorized()
    {
        var (db, controller, source, show, season, episode, _, logger) = await CreateControllerWithUnlockedEpisodeAsync(false);

        var result = await controller.Get("tvshowepisode", episode.Id);

        Assert.True(result is UnauthorizedObjectResult, logger.LastError ?? string.Empty);
    }

    [Fact]
    public async Task Get_TVShowEpisode_Unlocked_Returns_Ok()
    {
        var (db, controller, source, show, season, episode, _, logger) = await CreateControllerWithUnlockedEpisodeAsync(true);

        var result = await controller.Get("tvshowepisode", episode.Id);

        Assert.True(result is OkObjectResult, logger.LastError ?? string.Empty);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoTVShowEpisode>(okResult.Value);
        Assert.Equal(episode.Id, dto.Id);
    }

    [Fact]
    public async Task Get_Movie_Without_Access_Returns_Unauthorized()
    {
        var (db, controller, source, movie, _, _) = await CreateControllerWithUnlockedMovieAsync(false);

        var result = await controller.Get("movie", movie.Id);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Get_Movie_From_Unlocked_Collection_Returns_Ok()
    {
        var (db, controller, source, movie, _, _) = await CreateControllerWithUnlockedMovieAsync(true);

        var result = await controller.Get("movie", movie.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoMovie>(okResult.Value);
        Assert.Equal(movie.Id, dto.Id);
    }

    private async Task<(ApplicationDbContext db, ItemsController controller, MediaSource source, MovieCollection collection, ApplicationUser user, CapturingLogger<ItemsController> logger)> CreateControllerWithUnlockedCollectionAsync(bool unlock)
    {
        var ct = TestContext.Current.CancellationToken;
        var (db, controller, user, logger) = await CreateBaseControllerAsync();

        var source = new MediaSource { Name = "Movie Source", Path = "/m", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var collection = new MovieCollection { Name = "Collection", MediaSourceId = source.Id };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        if (unlock)
            db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = user.Id, MovieCollectionId = collection.Id });

        await db.SaveChangesAsync(ct);
        return (db, controller, source, collection, user, logger);
    }

    private async Task<(ApplicationDbContext db, ItemsController controller, MediaSource source, TVShow show, ApplicationUser user, CapturingLogger<ItemsController> logger)> CreateControllerWithUnlockedShowAsync(bool unlock)
    {
        var ct = TestContext.Current.CancellationToken;
        var (db, controller, user, logger) = await CreateBaseControllerAsync();

        var source = new MediaSource { Name = "Show Source", Path = "/s", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var show = new TVShow { Name = "Show", MediaSourceId = source.Id };
        db.TVShows.Add(show);
        await db.SaveChangesAsync(ct);

        if (unlock)
            db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = user.Id, TVShowId = show.Id });

        await db.SaveChangesAsync(ct);
        return (db, controller, source, show, user, logger);
    }

    private async Task<(ApplicationDbContext db, ItemsController controller, MediaSource source, TVShow show, TVShowSeason season, TVShowEpisode episode, ApplicationUser user, CapturingLogger<ItemsController> logger)> CreateControllerWithUnlockedEpisodeAsync(bool unlock)
    {
        var ct = TestContext.Current.CancellationToken;
        var (db, controller, user, logger) = await CreateBaseControllerAsync();

        var source = new MediaSource { Name = "Episode Source", Path = "/e", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var show = new TVShow { Name = "Show", MediaSourceId = source.Id };
        var season = new TVShowSeason { TVShow = show, MediaSourceId = source.Id };
        var episode = new TVShowEpisode { TVShowSeason = season, MediaSourceId = source.Id, Number = 1, Name = "Episode" };
        db.TVShows.Add(show);
        db.TVShowSeasons.Add(season);
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync(ct);

        if (unlock)
            db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = user.Id, TVShowId = show.Id });

        await db.SaveChangesAsync(ct);
        return (db, controller, source, show, season, episode, user, logger);
    }

    private async Task<(ApplicationDbContext db, ItemsController controller, MediaSource source, Movie movie, ApplicationUser user, CapturingLogger<ItemsController> logger)> CreateControllerWithUnlockedMovieAsync(bool unlock)
    {
        var ct = TestContext.Current.CancellationToken;
        var (db, controller, user, logger) = await CreateBaseControllerAsync();

        var source = new MediaSource { Name = "Movie Source", Path = "/mv", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var collection = new MovieCollection { Name = "Collection", MediaSourceId = source.Id };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        var movie = new Movie { Name = "Movie", MovieCollectionId = collection.Id, MediaSourceId = source.Id };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);

        if (unlock)
            db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = user.Id, MovieCollectionId = collection.Id });

        await db.SaveChangesAsync(ct);
        return (db, controller, source, movie, user, logger);
    }

    private async Task<(ApplicationDbContext db, ItemsController controller, ApplicationUser user, CapturingLogger<ItemsController> logger)> CreateBaseControllerAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:items-access-tests?mode=memory&cache=shared";
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser { Id = userId, UserName = "regular@test.com" };
        var fakeAuth = new FakeAuthService { CurrentUser = user };
        var logger = new CapturingLogger<ItemsController>();

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();

        var serviceProvider = services.BuildServiceProvider();
        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = serviceProvider.GetRequiredService<IUnlockedMediaService>();
        var recentEntryService = new RecentEntryService(db, fakeAuth, unlockedMediaService);
        var controller = new ItemsController(
            db,
            new SftpMediaSourceReader(),
            new MediaMetadataEditorService(db, null),
            recentEntryService,
            unlockedMediaService,
            fakeAuth,
            logger);

        return (db, controller, user, logger);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public string? LastError { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception != null)
                LastError = exception.ToString();
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
