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
    /// A file the server itself cannot read (missing permission of the service account) is a server-side
    /// problem. It must not leave the controller as 401, because a client reads that as an expired
    /// session, renews it and repeats the call - the very confusion A4 removes.
    /// </summary>
    [Fact]
    public async Task Stream_WhenTheFileCannotBeRead_ReturnsServerError()
    {
        var (controller, episode) = await CreateControllerWithLocalEpisodeAsync(new UnreadableFileReader());

        var result = await controller.StreamMediaItem("tvshowepisode", episode.Id);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task Download_WhenTheFileCannotBeRead_ReturnsServerError()
    {
        var (controller, episode) = await CreateControllerWithLocalEpisodeAsync(new UnreadableFileReader());

        var result = await controller.Download("tvshowepisode", episode.Id);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    /// <summary>
    /// Builds a controller with a local media source the user may read, holding one TV show episode
    /// whose media item points at a real file.
    /// </summary>
    /// <param name="reader">The media source reader to use; the real local reader when omitted.</param>
    /// <returns>The controller and the created episode.</returns>
    private async Task<(ItemsController Controller, TVShowEpisode Episode)> CreateControllerWithLocalEpisodeAsync(IMediaSourceReader? reader = null)
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
            reader ?? new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance),
            new MediaMetadataEditorService(db, null),
            new RecentEntryService(db, fakeAuth, unlockedMediaService),
            unlockedMediaService,
            fakeAuth,
            NullLogger<ItemsController>.Instance);

        return (controller, episode);
    }
}

/// <summary>
/// Media source reader whose file cannot be opened: <see cref="OpenFileStream"/> fails the way the file
/// system does when the service account may not read the file. The other members are not used by the
/// tests of this class.
/// </summary>
internal sealed class UnreadableFileReader : IMediaSourceReader
{
    /// <inheritdoc />
    public Stream? OpenFileStream(MediaCollection collection, string fileName)
        => throw new UnauthorizedAccessException($"Kein Lesezugriff auf '{fileName}'.");

    /// <inheritdoc />
    public IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source) => [];

    /// <inheritdoc />
    public IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection) => [];

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(MediaCollection collection, string fileName) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<string?> ReadFileAsync(MediaCollection collection, string fileName) => Task.FromResult<string?>(null);

    /// <inheritdoc />
    public Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName) => Task.FromResult<Stream?>(null);
}
