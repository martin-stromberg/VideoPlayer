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
public class LocalMediaSourceClassifierTests_AgeRating
{
    [Fact]
    public async Task ClassifyAllAsync_MovieNfoMpaa_PersistsAgeRating()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-agerating-movie-{Guid.NewGuid():N}");
        var movieDir = Path.Combine(rootDir, "RatedMovie");
        Directory.CreateDirectory(movieDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            var videoPath = Path.Combine(movieDir, "rated.mkv");
            File.WriteAllText(videoPath, "video");
            File.WriteAllText(Path.Combine(movieDir, "rated.nfo"),
                "<movie><title>Rated Movie</title><mpaa>FSK 16</mpaa></movie>");

            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("agerating-movie", ct);
            await SeedCollectionAsync(db, rootDir, movieDir, "RatedMovie", videoPath, ct);

            await classifier.ClassifyAllAsync(ct);

            var movie = await db.Movies.SingleOrDefaultAsync(m => m.Name == "Rated Movie", ct);
            Assert.NotNull(movie);
            Assert.Equal("FSK 16", movie!.AgeRatingRaw);
            Assert.Equal("FSK 16", movie.AgeRating);
            Assert.Equal(16, movie.AgeRatingMinAge);
            Assert.NotNull(movie.AgeRatingClassifiedAt);
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ClassifyAllAsync_TvShowNfoCertification_PersistsAgeRating()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-agerating-show-{Guid.NewGuid():N}");
        var showDir = Path.Combine(rootDir, "RatedShow");
        Directory.CreateDirectory(showDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            var videoPath = Path.Combine(showDir, "episode01.mkv");
            File.WriteAllText(videoPath, "video");
            File.WriteAllText(Path.Combine(showDir, "tvshow.nfo"),
                "<tvshow><title>Rated Show</title><certification>Germany:12</certification></tvshow>");
            File.WriteAllText(Path.Combine(showDir, "episode01.nfo"),
                "<episodedetails><title>Pilot</title><season>1</season><episode>1</episode></episodedetails>");

            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("agerating-show", ct);
            await SeedCollectionAsync(db, rootDir, showDir, "RatedShow", videoPath, ct);

            await classifier.ClassifyAllAsync(ct);

            var show = await db.TVShows.SingleOrDefaultAsync(s => s.Name == "Rated Show", ct);
            Assert.NotNull(show);
            Assert.Equal("Germany:12", show!.AgeRatingRaw);
            Assert.Equal("FSK 12", show.AgeRating);
            Assert.Equal(12, show.AgeRatingMinAge);
            Assert.NotNull(show.AgeRatingClassifiedAt);
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ClassifyAllAsync_ManuallyEditedMovie_KeepsExistingRating()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-agerating-manual-{Guid.NewGuid():N}");
        var movieDir = Path.Combine(rootDir, "ManualMovie");
        Directory.CreateDirectory(movieDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            var videoPath = Path.Combine(movieDir, "manual.mkv");
            File.WriteAllText(videoPath, "video");
            File.WriteAllText(Path.Combine(movieDir, "manual.nfo"),
                "<movie><title>Manual Movie</title><mpaa>FSK 18</mpaa></movie>");

            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("agerating-manual", ct);
            var collection = await SeedCollectionAsync(db, rootDir, movieDir, "ManualMovie", videoPath, ct);

            var mediaItem = await db.MediaItems.SingleAsync(mi => mi.MediaCollectionId == collection.Id, ct);
            var movie = new Movie
            {
                Name = "Manual Movie",
                MediaSourceId = collection.MediaSourceId,
                CollectionId = collection.Id,
                CreatedAt = DateTime.UtcNow,
                IsManuallyEdited = true,
                AgeRatingRaw = "PG",
                AgeRating = "PG",
                AgeRatingMinAge = 8
            };
            db.Movies.Add(movie);
            db.MovieMediaItems.Add(new MovieMediaItem { Movie = movie, MediaItem = mediaItem });
            await db.SaveChangesAsync(ct);

            await classifier.ClassifyAllAsync(ct);

            var reloaded = await db.Movies.SingleAsync(m => m.Id == movie.Id, ct);
            Assert.Equal("PG", reloaded.AgeRatingRaw);
            Assert.Equal("PG", reloaded.AgeRating);
            Assert.Equal(8, reloaded.AgeRatingMinAge);
            Assert.Null(reloaded.AgeRatingClassifiedAt);
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ClassifyAllAsync_NoNfoRating_TmdbFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-agerating-tmdb-{Guid.NewGuid():N}");
        var movieDir = Path.Combine(rootDir, "OnlineMovie");
        Directory.CreateDirectory(movieDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            var videoPath = Path.Combine(movieDir, "online.mkv");
            File.WriteAllText(videoPath, "video");
            File.WriteAllText(Path.Combine(movieDir, "online.nfo"),
                "<movie><title>Online Movie</title><uniqueid type=\"tmdb\">550</uniqueid></movie>");

            var fake = new FakeTmdbAgeRatingService(movieRating: new TmdbAgeRating("DE", "16"));
            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("agerating-tmdb", ct, fake);
            await SeedCollectionAsync(db, rootDir, movieDir, "OnlineMovie", videoPath, ct);

            await classifier.ClassifyAllAsync(ct);

            var movie = await db.Movies.SingleOrDefaultAsync(m => m.Name == "Online Movie", ct);
            Assert.NotNull(movie);
            Assert.Equal("DE:16", movie!.AgeRatingRaw);
            Assert.Equal("FSK 16", movie.AgeRating);
            Assert.Equal(16, movie.AgeRatingMinAge);
            Assert.NotNull(movie.AgeRatingClassifiedAt);
            Assert.Equal(1, fake.MovieCalls);
            Assert.Equal("550", fake.LastTmdbId);
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ClassifyAllAsync_NfoRating_TakesPriorityOverTmdb()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-agerating-prio-{Guid.NewGuid():N}");
        var movieDir = Path.Combine(rootDir, "PrioMovie");
        Directory.CreateDirectory(movieDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            var videoPath = Path.Combine(movieDir, "prio.mkv");
            File.WriteAllText(videoPath, "video");
            File.WriteAllText(Path.Combine(movieDir, "prio.nfo"),
                "<movie><title>Prio Movie</title><mpaa>FSK 6</mpaa></movie>");

            var fake = new FakeTmdbAgeRatingService(movieRating: new TmdbAgeRating("DE", "18"));
            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("agerating-prio", ct, fake);
            await SeedCollectionAsync(db, rootDir, movieDir, "PrioMovie", videoPath, ct);

            await classifier.ClassifyAllAsync(ct);

            var movie = await db.Movies.SingleOrDefaultAsync(m => m.Name == "Prio Movie", ct);
            Assert.NotNull(movie);
            Assert.Equal("FSK 6", movie!.AgeRating);
            Assert.Equal(6, movie.AgeRatingMinAge);
            Assert.Equal(0, fake.MovieCalls);
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    private static async Task<MediaCollection> SeedCollectionAsync(
        ApplicationDbContext db, string rootDir, string collectionDir, string name, string videoPath, CancellationToken ct)
    {
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
            Name = name,
            Path = collectionDir,
            CreatedAt = DateTime.UtcNow,
            MediaSource = source,
            MediaSourceId = source.Id,
            Classifyable = true,
            ClassifiedAt = null
        };
        db.MediaCollections.Add(collection);
        collection.MediaItems.Add(new MediaItem
        {
            Name = Path.GetFileName(videoPath),
            Path = videoPath,
            CreatedAt = DateTime.UtcNow,
            MediaCollection = collection,
            MediaCollectionId = collection.Id
        });
        await db.SaveChangesAsync(ct);
        return collection;
    }

    private static async Task<(ServiceProvider Provider, ApplicationDbContext Db, MediaSourceClassifier Classifier)> CreateContextAndServiceAsync(
        string testId, CancellationToken ct, ITmdbAgeRatingService? ageRatingService = null)
    {
        var connectionString = $"Data Source=file:{testId}?mode=memory&cache=shared";
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var services = new ServiceCollection();
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
        if (ageRatingService is not null)
            services.AddSingleton(ageRatingService);

        var serviceProvider = services.BuildServiceProvider();
        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, GenresChanged = false });
        await db.SaveChangesAsync(ct);

        var classifier = serviceProvider.GetRequiredService<MediaSourceClassifier>();
        return (serviceProvider, db, classifier);
    }
}
