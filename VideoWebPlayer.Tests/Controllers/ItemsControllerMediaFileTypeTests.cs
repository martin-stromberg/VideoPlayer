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
/// Stream and download must accept the same media types: <c>tvshow</c> means the episode for both, as
/// <c>docs/API.md</c> promises ("Es gelten dieselben Statuscodes wie bei .../stream"). Before both
/// endpoints resolved the file through one helper, only the stream endpoint normalized the type and a
/// download of <c>tvshow</c> answered 404.
/// </summary>
public sealed class ItemsControllerMediaFileTypeTests : IDisposable
{
    private readonly string _rootDir;
    private ServiceProvider? _serviceProvider;

    public ItemsControllerMediaFileTypeTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-mediafiletype-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _serviceProvider?.Dispose();
        try { Directory.Delete(_rootDir, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp dir may still be locked */ }
    }

    [Fact]
    public async Task Stream_WithTvShowType_ResolvesTheEpisodeFile()
    {
        var (controller, episode) = await CreateControllerWithLocalEpisodeAsync();

        var result = await controller.StreamMediaItem("tvshow", episode.Id);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("video/mp4", fileResult.ContentType);
        fileResult.FileStream.Dispose();
    }

    [Fact]
    public async Task Download_WithTvShowType_ResolvesTheEpisodeFile()
    {
        var (controller, episode) = await CreateControllerWithLocalEpisodeAsync();

        var result = await controller.Download("tvshow", episode.Id);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.Equal("episode.mp4", fileResult.FileDownloadName);
        fileResult.FileStream.Dispose();
    }

    [Fact]
    public async Task Download_WithUnsupportedType_ReturnsBadRequest()
    {
        var (controller, episode) = await CreateControllerWithLocalEpisodeAsync();

        var result = await controller.Download("moviecollection", episode.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Builds a controller with a local media source the user may read, holding one TV show episode
    /// whose media item points at a real file.
    /// </summary>
    /// <returns>The controller and the created episode.</returns>
    private async Task<(ItemsController Controller, TVShowEpisode Episode)> CreateControllerWithLocalEpisodeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var filePath = Path.Combine(_rootDir, "episode.mp4");
        await File.WriteAllBytesAsync(filePath, [1, 2, 3], ct);

        var connectionString = $"Data Source=file:items-mediafiletype-{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "mediafiletype@test.com" };
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

        var source = new MediaSource
        {
            Name = "Local Source",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

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

        var show = new TVShow { Name = "Serie", MediaSourceId = source.Id };
        var season = new TVShowSeason { TVShow = show, MediaSourceId = source.Id, Name = "Staffel 1" };
        var episode = new TVShowEpisode { TVShowSeason = season, MediaSourceId = source.Id, Number = 1, Name = "Folge 1" };
        db.TVShows.Add(show);
        db.TVShowSeasons.Add(season);
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync(ct);

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

        db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisodeId = episode.Id, MediaItemId = mediaItem.Id });
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = serviceProvider.GetRequiredService<IUnlockedMediaService>();
        var controller = new ItemsController(
            db,
            new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance),
            new MediaMetadataEditorService(db, null),
            new RecentEntryService(db, fakeAuth, unlockedMediaService),
            unlockedMediaService,
            fakeAuth,
            NullLogger<ItemsController>.Instance);

        return (controller, episode);
    }
}
