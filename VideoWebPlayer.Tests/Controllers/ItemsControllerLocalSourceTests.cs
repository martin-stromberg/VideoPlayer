using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

public class ItemsControllerLocalSourceTests : IDisposable
{
    private readonly string _rootDir;
    private ServiceProvider? _serviceProvider;

    public ItemsControllerLocalSourceTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-localstream-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        try { Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task StreamMediaItem_LocalSource_ReturnsFileStreamResult()
    {
        var fileBytes = new byte[] { 10, 20, 30, 40, 50 };
        var filePath = Path.Combine(_rootDir, "movie.mp4");
        await File.WriteAllBytesAsync(filePath, fileBytes, TestContext.Current.CancellationToken);

        var (controller, movie) = await CreateControllerWithLocalMovieAsync(filePath, grantSourceAccess: true);

        var result = await controller.StreamMediaItem("movie", movie.Id);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("video/mp4", fileResult.ContentType);
        Assert.IsType<FileStream>(fileResult.FileStream);
        Assert.True(fileResult.FileStream.CanSeek);

        using (fileResult.FileStream)
        using (var ms = new MemoryStream())
        {
            await fileResult.FileStream.CopyToAsync(ms, TestContext.Current.CancellationToken);
            Assert.Equal(fileBytes, ms.ToArray());
        }
    }

    [Fact]
    public async Task Download_LocalMovie_ReturnsFileWithDownloadName()
    {
        var filePath = Path.Combine(_rootDir, "movie.mp4");
        await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        var (controller, movie) = await CreateControllerWithLocalMovieAsync(filePath, grantSourceAccess: true);

        var result = await controller.Download("movie", movie.Id);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.Equal("movie.mp4", fileResult.FileDownloadName);
        fileResult.FileStream.Dispose();
    }

    [Fact]
    public async Task StreamMediaItem_LocalMovie_WithoutSourceAccess_ReturnsUnauthorized()
    {
        var filePath = Path.Combine(_rootDir, "movie.mp4");
        await File.WriteAllBytesAsync(filePath, new byte[] { 1 }, TestContext.Current.CancellationToken);

        var (controller, movie) = await CreateControllerWithLocalMovieAsync(filePath, grantSourceAccess: false);

        var result = await controller.StreamMediaItem("movie", movie.Id);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private async Task<(ItemsController Controller, Movie Movie)> CreateControllerWithLocalMovieAsync(string filePath, bool grantSourceAccess)
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=file:items-localstream-{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser { Id = userId, UserName = "stream@test.com" };
        var fakeAuth = new FakeAuthService { CurrentUser = user };

        var services = new ServiceCollection();
        // Keeper-Connection im Provider registrieren, damit sie zusammen mit ihm disposed wird
        // und die Shared-In-Memory-Datenbank beim Testende geschlossen wird.
        services.AddSingleton(keeperConnection);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();

        var serviceProvider = services.BuildServiceProvider();
        _serviceProvider = serviceProvider;
        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);

        var source = new MediaSource
        {
            Name = "Local Source",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        if (grantSourceAccess)
            db.MediaSourceUsers.Add(new MediaSourceUser { UserId = user.Id, MediaSourceId = source.Id });

        var collection = new MediaCollection
        {
            Name = "root",
            Path = _rootDir,
            CreatedAt = DateTime.UtcNow,
            MediaSource = source,
            MediaSourceId = source.Id
        };
        db.MediaCollections.Add(collection);

        var movieCollection = new MovieCollection { Name = "Collection", MediaSourceId = source.Id };
        db.MovieCollections.Add(movieCollection);
        await db.SaveChangesAsync(ct);

        var movie = new Movie { Name = "Movie", MovieCollectionId = movieCollection.Id, MediaSourceId = source.Id };
        db.Movies.Add(movie);

        var mediaItem = new MediaItem
        {
            Name = Path.GetFileName(filePath),
            Path = filePath,
            CreatedAt = DateTime.UtcNow,
            MediaCollection = collection,
            MediaCollectionId = collection.Id
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = mediaItem.Id });
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = serviceProvider.GetRequiredService<IUnlockedMediaService>();
        var recentEntryService = new RecentEntryService(db, fakeAuth, unlockedMediaService);
        var controller = new ItemsController(
            db,
            new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance),
            new MediaMetadataEditorService(db, null),
            recentEntryService,
            unlockedMediaService,
            fakeAuth,
            NullLogger<ItemsController>.Instance);

        return (controller, movie);
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
