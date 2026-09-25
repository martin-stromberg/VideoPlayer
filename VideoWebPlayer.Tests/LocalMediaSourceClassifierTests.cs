using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Services.EpisodeBackgroundImage;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

[Collection(MediaSourceClassifierCollection.Name)]
public class LocalMediaSourceClassifierTests
{
    [Fact]
    public async Task ClassifyAllAsync_ClassifiesTvShow_FromLocalSource()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-classifier-local-{Guid.NewGuid():N}");
        var showDir = Path.Combine(rootDir, "TestShow");
        Directory.CreateDirectory(showDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            var videoPath = Path.Combine(showDir, "episode01.mkv");
            File.WriteAllText(videoPath, "video");
            File.WriteAllText(Path.Combine(showDir, "tvshow.nfo"),
                "<tvshow><title>Local Test Show</title><genre>Drama</genre><premiered>2020-01-02</premiered></tvshow>");
            File.WriteAllText(Path.Combine(showDir, "episode01.nfo"),
                "<episodedetails><title>Pilot</title><season>1</season><episode>1</episode><aired>2020-01-05</aired></episodedetails>");

            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("local-classifier-tvshow", ct);

            var source = new MediaSource
            {
                Name = "Local Source",
                Path = rootDir,
                SourceType = MediaSourceType.LocalDirectory,
                CreatedAt = DateTime.UtcNow
            };
            db.MediaSources.Add(source);
            var collection = new MediaCollection
            {
                Name = "TestShow",
                Path = showDir,
                CreatedAt = DateTime.UtcNow,
                MediaSource = source,
                MediaSourceId = source.Id,
                Classifyable = true,
                ClassifiedAt = null
            };
            db.MediaCollections.Add(collection);
            collection.MediaItems.Add(new MediaItem
            {
                Name = "episode01.mkv",
                Path = videoPath,
                CreatedAt = DateTime.UtcNow,
                MediaCollection = collection,
                MediaCollectionId = collection.Id
            });
            await db.SaveChangesAsync(ct);

            await classifier.ClassifyAllAsync(ct);

            var show = await db.TVShows.SingleOrDefaultAsync(s => s.Name == "Local Test Show", ct);
            Assert.NotNull(show);
            Assert.Equal(collection.Id, show!.CollectionId);
            Assert.Equal("Drama", show.GenreNames);

            var season = await db.TVShowSeasons.SingleOrDefaultAsync(s => s.TVShowId == show.Id, ct);
            Assert.NotNull(season);
            Assert.Equal("Staffel 01", season!.Name);

            var episode = await db.TVShowEpisodes.SingleOrDefaultAsync(e => e.TVShowSeasonId == season.Id, ct);
            Assert.NotNull(episode);
            Assert.Equal("Pilot", episode!.Name);
            Assert.Equal(1, episode.Number);
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    private static async Task<(ServiceProvider Provider, ApplicationDbContext Db, MediaSourceClassifier Classifier)> CreateContextAndServiceAsync(string testId, CancellationToken ct)
    {
        var connectionString = $"Data Source=file:{testId}?mode=memory&cache=shared";
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var services = new ServiceCollection();
        // Keeper-Connection im Provider registrieren, damit sie zusammen mit ihm disposed wird
        // und die Shared-In-Memory-Datenbank beim Testende geschlossen wird.
        services.AddSingleton(keeperConnection);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance));
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<RecentEntryService>();
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();
        services.AddScoped<MediaSourceClassifier>();
        services.AddScoped<HttpClient>(_ => new HttpClient());
        services.AddMemoryCache();
        services.AddScoped<EpisodeBackgroundImageGenerator>();
        services.AddScoped<EpisodeBackgroundImageService>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<EpisodeBackgroundImageOptions>>(
            Microsoft.Extensions.Options.Options.Create(new EpisodeBackgroundImageOptions()));
        services.AddSingleton<ILogger<EpisodeBackgroundImageGenerator>>(NullLogger<EpisodeBackgroundImageGenerator>.Instance);
        services.AddSingleton<ILogger<EpisodeBackgroundImageService>>(NullLogger<EpisodeBackgroundImageService>.Instance);
        services.AddSingleton<IAuthService, TestAuthService>();
        services.AddSingleton<ILogger<MediaSourceClassifier>>(NullLogger<MediaSourceClassifier>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);

        var serviceProvider = services.BuildServiceProvider();
        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, GenresChanged = false });
        await db.SaveChangesAsync(ct);

        var classifier = serviceProvider.GetRequiredService<MediaSourceClassifier>();
        return (serviceProvider, db, classifier);
    }
}
