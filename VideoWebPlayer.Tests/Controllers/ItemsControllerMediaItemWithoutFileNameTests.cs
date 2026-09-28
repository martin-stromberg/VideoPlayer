using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Covers a media item whose <see cref="MediaItem.Path"/> carries no file name portion (e.g. a scan
/// artifact pointing at a directory). Both readers must resolve this to <c>null</c> and the endpoint to
/// 404, not to an unhandled exception - see <c>continue.md</c> for the removed dead substitute file
/// name this replaces and the risk it used to mask for the SFTP reader.
/// </summary>
public sealed class ItemsControllerMediaItemWithoutFileNameTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    /// <inheritdoc />
    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task Stream_WithLocalReader_MediaItemPathWithoutFileName_Returns_NotFound()
    {
        var (controller, movie) = await CreateControllerAsync(new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance));

        var result = await controller.StreamMediaItem("movie", movie.Id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Stream_WithSftpReader_MediaItemPathWithoutFileName_Returns_NotFound()
    {
        var (controller, movie) = await CreateControllerAsync(new SftpMediaSourceReader());

        var result = await controller.StreamMediaItem("movie", movie.Id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>
    /// Builds a controller with the given reader and a movie whose media item's path ends in a
    /// directory separator, so <c>Path.GetFileName</c> resolves to an empty file name.
    /// </summary>
    /// <param name="reader">The media source reader under test.</param>
    /// <returns>The ready-to-use controller and the created movie without a resolvable video file.</returns>
    private async Task<(ItemsController Controller, Movie Movie)> CreateControllerAsync(IMediaSourceReader reader)
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=file:items-no-filename-{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "no-filename@test.com" };
        var fakeAuth = new FakeAuthService { CurrentUser = user };

        var services = new ServiceCollection();
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

        var source = new MediaSource { Name = "Quelle", Path = "/m", Host = "host.invalid", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = user.Id, MediaSourceId = source.Id });

        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = source.Id };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        var mediaCollection = new MediaCollection
        {
            Name = "root",
            Path = "/m",
            CreatedAt = DateTime.UtcNow,
            MediaSourceId = source.Id
        };
        db.MediaCollections.Add(mediaCollection);
        await db.SaveChangesAsync(ct);

        var createdMovie = new Movie { Name = "Film ohne Dateinamen", MovieCollectionId = collection.Id, MediaSourceId = source.Id };
        db.Movies.Add(createdMovie);
        await db.SaveChangesAsync(ct);

        var mediaItem = new MediaItem
        {
            Name = "ohne-dateinamen",
            Path = mediaCollection.Path + "/",
            CreatedAt = DateTime.UtcNow,
            MediaCollectionId = mediaCollection.Id
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = createdMovie.Id, MediaItemId = mediaItem.Id });
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = serviceProvider.GetRequiredService<IUnlockedMediaService>();
        var controller = new ItemsController(
            db,
            reader,
            new MediaMetadataEditorService(db, null),
            new RecentEntryService(db, fakeAuth, unlockedMediaService),
            unlockedMediaService,
            fakeAuth,
            NullLogger<ItemsController>.Instance);

        return (controller, createdMovie);
    }
}
