using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class EpisodesControllerBackgroundImageTests
{
    [Fact]
    public async Task GetBackgroundImage_WhenNotLoggedIn_ReturnsUnauthorized()
    {
        var (db, keeper) = CreateDb();
        using (keeper)
        await using (db)
        {
            var episode = await CreateEpisodeAsync(db);
            var controller = CreateController(db, loggedIn: false);

            var result = await controller.GetBackgroundImage(episode.Id);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }

    [Fact]
    public async Task GetBackgroundImage_WhenEpisodeUnknown_ReturnsNotFound()
    {
        var (db, keeper) = CreateDb();
        using (keeper)
        await using (db)
        {
            var controller = CreateController(db, loggedIn: true);

            var result = await controller.GetBackgroundImage(episodeId: 999999);

            Assert.IsType<NotFoundResult>(result);
        }
    }

    [Fact]
    public async Task GetBackgroundImage_WhenGeneratedImageExists_ReturnsGeneratedImage()
    {
        var (db, keeper) = CreateDb();
        using (keeper)
        await using (db)
        {
            var episode = await CreateEpisodeAsync(db);
            var generated = await CreatePictureAsync(db, isGeneratedBackground: true, data: new byte[] { 9, 9, 9 }, contentType: "image/jpeg");
            episode.GeneratedBackgroundPictureId = generated.Id;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var controller = CreateController(db, loggedIn: true);

            var result = await controller.GetBackgroundImage(episode.Id);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal(generated.Data, fileResult.FileContents);
            Assert.Equal("image/jpeg", fileResult.ContentType);
        }
    }

    [Fact]
    public async Task GetBackgroundImage_WhenNoGeneratedImage_FallsBackToBannerOrFanart()
    {
        var (db, keeper) = CreateDb();
        using (keeper)
        await using (db)
        {
            var episode = await CreateEpisodeAsync(db);
            var banner = await CreatePictureAsync(db, isGeneratedBackground: false, data: new byte[] { 1, 2, 3 }, contentType: "image/png");
            episode.BannerPictureId = banner.Id;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var controller = CreateController(db, loggedIn: true);

            var result = await controller.GetBackgroundImage(episode.Id);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal(banner.Data, fileResult.FileContents);
            Assert.Equal("image/png", fileResult.ContentType);
        }
    }

    [Fact]
    public async Task GetBackgroundImage_WhenNoImagesAvailable_ReturnsPlaceholder()
    {
        var (db, keeper) = CreateDb();
        using (keeper)
        await using (db)
        {
            var episode = await CreateEpisodeAsync(db);
            var controller = CreateController(db, loggedIn: true);

            var result = await controller.GetBackgroundImage(episode.Id);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("image/png", fileResult.ContentType);
            Assert.NotEmpty(fileResult.FileContents);
        }
    }

    private static EpisodesController CreateController(ApplicationDbContext db, bool loggedIn)
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.CurrentUser).Returns(loggedIn ? new ApplicationUser { UserName = "tester" } : null);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new EpisodesController(db, cache, authService.Object, NullLogger<EpisodesController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static (ApplicationDbContext Db, SqliteConnection Keeper) CreateDb()
    {
        var connectionString = $"Data Source=file:episodes-controller-tests-{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        var db = new ApplicationDbContext(options, new EventManager());
        db.Database.EnsureCreated();
        return (db, keeper);
    }

    private static async Task<TVShowEpisode> CreateEpisodeAsync(ApplicationDbContext db)
    {
        var show = new TVShow { Name = "Testshow", CreatedAt = DateTime.UtcNow };
        db.TVShows.Add(show);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var season = new TVShowSeason { Name = "Staffel 01", TVShowId = show.Id, CreatedAt = DateTime.UtcNow };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var episode = new TVShowEpisode { Name = "Testepisode", Number = 1, TVShowSeasonId = season.Id, CreatedAt = DateTime.UtcNow };
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return episode;
    }

    private static async Task<Picture> CreatePictureAsync(ApplicationDbContext db, bool isGeneratedBackground, byte[] data, string contentType)
    {
        var source = new MediaSource { Name = "Source", Path = "/source", Host = "localhost", Port = 22, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collection = new MediaCollection { Name = "Collection", Path = "/source/collection", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mediaItem = new MediaItem { Name = "image.png", Path = "/source/collection/image.png", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var picture = new Picture { MediaItemId = mediaItem.Id, Type = "fanart", Data = data, ContentType = contentType, IsGeneratedBackground = isGeneratedBackground };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return picture;
    }
}
