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
public class LocalMediaSourceClassifierTests_Trailer
{
    [Fact]
    public async Task ClassifyAllAsync_TrailerSuffix_LinksToMovie_NotAsMovie()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-classifier-trailer-{Guid.NewGuid():N}");
        var movieDir = Path.Combine(rootDir, "Film (2020)");
        Directory.CreateDirectory(movieDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            File.WriteAllText(Path.Combine(movieDir, "Film (2020).mkv"), "video");
            File.WriteAllText(Path.Combine(movieDir, "Film (2020)-trailer.mkv"), "video");
            File.WriteAllText(Path.Combine(movieDir, "Film (2020).nfo"), "<movie><title>Mein Film</title></movie>");

            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("local-classifier-trailer-suffix", ct);
            var (collection, _) = await SeedCollectionAsync(db, movieDir, "Film (2020)", null, ct,
                "Film (2020).mkv", "Film (2020)-trailer.mkv");

            await classifier.ClassifyAllAsync(ct);

            var movie = await db.Movies.SingleOrDefaultAsync(m => m.Name == "Mein Film", ct);
            Assert.NotNull(movie);
            var links = await db.MovieMediaItems.Where(l => l.MovieId == movie!.Id).ToListAsync(ct);
            Assert.Equal(2, links.Count);
            var trailerLink = links.Single(l => l.IsTrailer);
            var trailerItem = await db.MediaItems.FindAsync([trailerLink.MediaItemId], ct);
            Assert.EndsWith("-trailer.mkv", trailerItem!.Path, StringComparison.OrdinalIgnoreCase);
            Assert.Single(await db.Movies.ToListAsync(ct));
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ClassifyAllAsync_TrailersFolder_LinksToParentMovie()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-classifier-trailerdir-{Guid.NewGuid():N}");
        var movieDir = Path.Combine(rootDir, "Film (2020)");
        var trailerDir = Path.Combine(movieDir, "trailers");
        Directory.CreateDirectory(trailerDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            File.WriteAllText(Path.Combine(movieDir, "Film (2020).mkv"), "video");
            File.WriteAllText(Path.Combine(movieDir, "Film (2020).nfo"), "<movie><title>Mein Film</title></movie>");
            File.WriteAllText(Path.Combine(trailerDir, "preview.mkv"), "video");

            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("local-classifier-trailer-folder", ct);
            var (parent, _) = await SeedCollectionAsync(db, movieDir, "Film (2020)", null, ct, "Film (2020).mkv");
            await SeedCollectionAsync(db, trailerDir, "trailers", parent.Id, ct, "preview.mkv");

            await classifier.ClassifyAllAsync(ct);

            var movie = await db.Movies.SingleOrDefaultAsync(m => m.Name == "Mein Film", ct);
            Assert.NotNull(movie);
            var links = await db.MovieMediaItems.Where(l => l.MovieId == movie!.Id).ToListAsync(ct);
            Assert.Equal(2, links.Count);
            var trailerLink = links.Single(l => l.IsTrailer);
            var trailerItem = await db.MediaItems.FindAsync([trailerLink.MediaItemId], ct);
            Assert.EndsWith("preview.mkv", trailerItem!.Path);
            Assert.Single(await db.Movies.ToListAsync(ct));
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ClassifyAllAsync_TrailerSuffix_IsNeverRepresentativeMediaItem()
    {
        var ct = TestContext.Current.CancellationToken;
        var rootDir = Path.Combine(Path.GetTempPath(), $"vwp-classifier-trailermain-{Guid.NewGuid():N}");
        var movieDir = Path.Combine(rootDir, "Film (2020)");
        Directory.CreateDirectory(movieDir);
        ServiceProvider? serviceProvider = null;
        try
        {
            File.WriteAllText(Path.Combine(movieDir, "Film (2020).mkv"), "video");
            File.WriteAllText(Path.Combine(movieDir, "Film (2020)-trailer.mkv"), "video");
            File.WriteAllText(Path.Combine(movieDir, "Film (2020).nfo"), "<movie><title>Mein Film</title></movie>");
            // Altstand: Trailer-Link bereits als regulärer Link vorhanden → wird repariert.
            (serviceProvider, var db, var classifier) = await CreateContextAndServiceAsync("local-classifier-trailer-repair", ct);
            var (collection, items) = await SeedCollectionAsync(db, movieDir, "Film (2020)", null, ct,
                "Film (2020).mkv", "Film (2020)-trailer.mkv");

            await classifier.ClassifyAllAsync(ct);

            var movie = await db.Movies.SingleAsync(m => m.Name == "Mein Film", ct);
            var mainLink = await db.MovieMediaItems.SingleOrDefaultAsync(l => l.MovieId == movie.Id && !l.IsTrailer, ct);
            Assert.NotNull(mainLink);
            var mainItem = await db.MediaItems.FindAsync([mainLink!.MediaItemId], ct);
            Assert.DoesNotContain("-trailer", Path.GetFileName(mainItem!.Path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            serviceProvider?.Dispose();
            try { Directory.Delete(rootDir, recursive: true); } catch { }
        }
    }

    private static async Task<(MediaCollection Collection, List<MediaItem> Items)> SeedCollectionAsync(
        ApplicationDbContext db, string dir, string name, long? parentId, CancellationToken ct, params string[] fileNames)
    {
        var source = new MediaSource { Name = "Local Source", Path = dir, SourceType = MediaSourceType.LocalDirectory, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);
        var collection = new MediaCollection
        {
            Name = name,
            Path = dir,
            CreatedAt = DateTime.UtcNow,
            MediaSource = source,
            MediaSourceId = source.Id,
            ParentMediaCollectionId = parentId,
            Classifyable = true,
            ClassifiedAt = null,
            Changed = true
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        var items = new List<MediaItem>();
        foreach (var fileName in fileNames)
        {
            var item = new MediaItem
            {
                Name = fileName,
                Path = Path.Combine(dir, fileName),
                CreatedAt = DateTime.UtcNow,
                MediaCollection = collection,
                MediaCollectionId = collection.Id
            };
            db.MediaItems.Add(item);
            items.Add(item);
        }
        await db.SaveChangesAsync(ct);
        return (collection, items);
    }

    private static async Task<(ServiceProvider Provider, ApplicationDbContext Db, MediaSourceClassifier Classifier)> CreateContextAndServiceAsync(string testId, CancellationToken ct)
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

        var serviceProvider = services.BuildServiceProvider();
        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, GenresChanged = false });
        await db.SaveChangesAsync(ct);

        var classifier = serviceProvider.GetRequiredService<MediaSourceClassifier>();
        return (serviceProvider, db, classifier);
    }
}
