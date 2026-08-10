using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.EpisodeBackgroundImage;
using Xunit;

namespace VideoWebPlayer.Tests.Services.EpisodeBackgroundImage;

public class EpisodeBackgroundImageServiceTests
{
    [Fact]
    public async Task Test_EnsureBackgroundImage_LazyLoads_OnFirstCall()
    {
        var (db, keeper, _) = CreateMockDbContext();
        using (keeper)
        await using (db)
        {
            var episode = await CreateTestEpisodeAsync(db, withFanart: true);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(db, cache);

            var picture = await service.EnsureBackgroundImageAsync(episode, TestContext.Current.CancellationToken);

            Assert.NotNull(picture);
            Assert.True(picture!.IsGeneratedBackground);
            Assert.Equal(episode.Id, picture.EpisodeId);

            var reloaded = await db.TVShowEpisodes.AsNoTracking().FirstAsync(e => e.Id == episode.Id, TestContext.Current.CancellationToken);
            Assert.Equal(picture.Id, reloaded.GeneratedBackgroundPictureId);
            Assert.False(reloaded.BackgroundImageRequiresUpdate);
            Assert.NotNull(reloaded.BackgroundImageGeneratedAt);
        }
    }

    [Fact]
    public async Task Test_EnsureBackgroundImage_UsesCached_OnSubsequentCall()
    {
        var (db, keeper, _) = CreateMockDbContext();
        using (keeper)
        await using (db)
        {
            var episode = await CreateTestEpisodeAsync(db, withFanart: true);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(db, cache);

            var firstPicture = await service.EnsureBackgroundImageAsync(episode, TestContext.Current.CancellationToken);
            Assert.NotNull(firstPicture);

            // Fanart-Daten entfernen, um zu beweisen, dass der zweite Aufruf nicht erneut generiert.
            var fanart = await db.Pictures.FirstAsync(p => p.Id == episode.FanartPictureId!.Value, TestContext.Current.CancellationToken);
            fanart.Data = Array.Empty<byte>();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var reloadedEpisode = await db.TVShowEpisodes.AsNoTracking().FirstAsync(e => e.Id == episode.Id, TestContext.Current.CancellationToken);
            var secondPicture = await service.EnsureBackgroundImageAsync(reloadedEpisode, TestContext.Current.CancellationToken);

            Assert.NotNull(secondPicture);
            Assert.Equal(firstPicture!.Id, secondPicture!.Id);
        }
    }

    [Fact]
    public async Task Test_EnsureBackgroundImage_ThreadSafe_ParallelRequests()
    {
        var (seedDb, keeper, connectionString) = CreateMockDbContext();
        long episodeId;
        await using (seedDb)
        {
            var episode = await CreateTestEpisodeAsync(seedDb, withFanart: true);
            episodeId = episode.Id;
        }

        using (keeper)
        {
            var sharedCache = new MemoryCache(new MemoryCacheOptions());
            var tasks = new List<Task<Picture?>>();

            for (var i = 0; i < 10; i++)
            {
                var db = CreateDbContextForConnection(connectionString);
                var service = CreateService(db, sharedCache);
                tasks.Add(RunAndDisposeAsync(db, service, episodeId));
            }

            var results = await Task.WhenAll(tasks);

            Assert.All(results, r => Assert.NotNull(r));
            var distinctIds = results.Select(r => r!.Id).Distinct().ToList();
            Assert.Single(distinctIds);

            await using var verifyDb = CreateDbContextForConnection(connectionString);
            var generatedCount = await verifyDb.Pictures
                .CountAsync(p => p.EpisodeId == episodeId && p.IsGeneratedBackground, TestContext.Current.CancellationToken);
            Assert.Equal(1, generatedCount);
        }

        static async Task<Picture?> RunAndDisposeAsync(ApplicationDbContext db, EpisodeBackgroundImageService service, long episodeId)
        {
            await using (db)
            {
                var episode = await db.TVShowEpisodes.FirstAsync(e => e.Id == episodeId, TestContext.Current.CancellationToken);
                return await service.EnsureBackgroundImageAsync(episode, TestContext.Current.CancellationToken);
            }
        }
    }

    [Fact]
    public async Task Test_MarkBackgroundImageForUpdate_SetsFlag_OnNewFanart()
    {
        var (db, keeper, _) = CreateMockDbContext();
        using (keeper)
        await using (db)
        {
            var episode = await CreateTestEpisodeAsync(db, withFanart: true);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(db, cache);
            await service.EnsureBackgroundImageAsync(episode, TestContext.Current.CancellationToken);

            await service.MarkBackgroundImageForUpdateAsync(episode.Id, TestContext.Current.CancellationToken);

            var reloaded = await db.TVShowEpisodes.AsNoTracking().FirstAsync(e => e.Id == episode.Id, TestContext.Current.CancellationToken);
            Assert.True(reloaded.BackgroundImageRequiresUpdate);
        }
    }

    [Fact]
    public async Task Test_EnsureBackgroundImage_Regenerates_WhenFlagSet()
    {
        var (db, keeper, _) = CreateMockDbContext();
        using (keeper)
        await using (db)
        {
            var episode = await CreateTestEpisodeAsync(db, withFanart: true);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(db, cache);
            var firstPicture = await service.EnsureBackgroundImageAsync(episode, TestContext.Current.CancellationToken);

            await service.MarkBackgroundImageForUpdateAsync(episode.Id, TestContext.Current.CancellationToken);

            var fanart = await db.Pictures.FirstAsync(p => p.Id == episode.FanartPictureId!.Value, TestContext.Current.CancellationToken);
            fanart.Data = CreateTestImageBytes(SixLabors.ImageSharp.Color.Purple);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var reloadedEpisode = await db.TVShowEpisodes.AsNoTracking().FirstAsync(e => e.Id == episode.Id, TestContext.Current.CancellationToken);
            var secondPicture = await service.EnsureBackgroundImageAsync(reloadedEpisode, TestContext.Current.CancellationToken);

            Assert.NotNull(secondPicture);
            Assert.NotEqual(firstPicture!.Id, secondPicture!.Id);

            var finalEpisode = await db.TVShowEpisodes.AsNoTracking().FirstAsync(e => e.Id == episode.Id, TestContext.Current.CancellationToken);
            Assert.Equal(secondPicture.Id, finalEpisode.GeneratedBackgroundPictureId);
            Assert.False(finalEpisode.BackgroundImageRequiresUpdate);

            var obsoletePicture = await db.Pictures.AsNoTracking().FirstOrDefaultAsync(p => p.Id == firstPicture.Id, TestContext.Current.CancellationToken);
            Assert.Null(obsoletePicture);
        }
    }

    [Fact]
    public async Task Test_EnsureBackgroundImage_ReturnsFallback_OnGenerationError()
    {
        var (db, keeper, _) = CreateMockDbContext();
        using (keeper)
        await using (db)
        {
            var episode = await CreateTestEpisodeAsync(db, withFanart: true, fanartData: new byte[] { 1, 2, 3, 4 });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(db, cache);

            var picture = await service.EnsureBackgroundImageAsync(episode, TestContext.Current.CancellationToken);

            Assert.Null(picture);
            var reloaded = await db.TVShowEpisodes.AsNoTracking().FirstAsync(e => e.Id == episode.Id, TestContext.Current.CancellationToken);
            Assert.Null(reloaded.GeneratedBackgroundPictureId);
        }
    }

    private static (ApplicationDbContext Db, SqliteConnection Keeper, string ConnectionString) CreateMockDbContext()
    {
        var connectionString = $"Data Source=file:epbgimg-tests-{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        var db = CreateDbContextForConnection(connectionString);
        db.Database.EnsureCreated();
        return (db, keeper, connectionString);
    }

    private static ApplicationDbContext CreateDbContextForConnection(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        return new ApplicationDbContext(options, new VideoWebPlayer.Services.EventManager());
    }

    private static EpisodeBackgroundImageService CreateService(ApplicationDbContext db, IMemoryCache cache, EpisodeBackgroundImageOptions? options = null)
    {
        var resolvedOptions = Options.Create(options ?? new EpisodeBackgroundImageOptions());
        var generator = new VideoWebPlayer.Services.EpisodeBackgroundImage.EpisodeBackgroundImageGenerator(
            resolvedOptions,
            NullLogger<VideoWebPlayer.Services.EpisodeBackgroundImage.EpisodeBackgroundImageGenerator>.Instance);
        return new EpisodeBackgroundImageService(db, generator, cache, resolvedOptions, NullLogger<EpisodeBackgroundImageService>.Instance);
    }

    private static async Task<TVShowEpisode> CreateTestEpisodeAsync(ApplicationDbContext db, bool withFanart, byte[]? fanartData = null)
    {
        var show = new TVShow { Name = "Testshow", CreatedAt = DateTime.UtcNow };
        db.TVShows.Add(show);
        await db.SaveChangesAsync();

        var season = new TVShowSeason { Name = "Staffel 01", TVShowId = show.Id, CreatedAt = DateTime.UtcNow };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync();

        var episode = new TVShowEpisode { Name = "Testepisode", Number = 1, TVShowSeasonId = season.Id, CreatedAt = DateTime.UtcNow };

        if (withFanart)
        {
            var fanartPicture = await CreateTestPictureAsync(db, "fanart", fanartData ?? CreateTestImageBytes());
            episode.FanartPictureId = fanartPicture.Id;
        }

        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync();
        return episode;
    }

    private static async Task<Picture> CreateTestPictureAsync(ApplicationDbContext db, string type, byte[] data)
    {
        var source = new MediaSource { Name = "Source", Path = "/source", Host = "localhost", Port = 22, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        var collection = new MediaCollection { Name = "Collection", Path = "/source/collection", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync();

        var mediaItem = new MediaItem { Name = "image.png", Path = "/source/collection/image.png", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync();

        var picture = new Picture { MediaItemId = mediaItem.Id, Type = type, Data = data, ContentType = "image/png" };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync();
        return picture;
    }

    private static byte[] CreateTestImageBytes(Color? color = null)
    {
        using var image = new Image<Rgba32>(64, 64, (color ?? Color.Teal).ToPixel<Rgba32>());
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
