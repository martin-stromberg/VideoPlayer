using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Services.EpisodeBackgroundImage;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

[Collection(MediaSourceClassifierCollection.Name)]
public class MediaSourceClassifierBackgroundImageTests
{
    [Fact]
    public async Task ClassifyAllAsync_MarksBackgroundForUpdate_WhenNewFanartFileAppearsForEpisodeWithExistingGeneratedBackground()
    {
        var connectionString = "Data Source=file:classifier-background-fanart?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var rootPath = "/media";
        var showName = "TestShow";
        var reader = new SeriesSftpMediaSourceReader(rootPath, showName, seasonCount: 1, episodesPerSeason: 1);

        var serviceProvider = BuildServiceProvider(connectionString, reader);
        await SeedMediaSourceAsync(serviceProvider, rootPath);

        var ct = TestContext.Current.CancellationToken;
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

        await ScanShowAndSeasonAsync(scanner, ct);
        await classifier.ClassifyAllAsync(ct);

        var episode = await db.TVShowEpisodes.SingleAsync(ct);
        Assert.NotNull(episode.PosterPictureId);

        // Simulate a background image that was already generated in the past. Reuse the same
        // ApplicationDbContext (and thus the same tracked episode instance) as the scanner/classifier
        // so the subsequent classification pass observes this state instead of a stale in-memory copy.
        episode.GeneratedBackgroundPictureId = episode.PosterPictureId;
        episode.BackgroundImageRequiresUpdate = false;
        await db.SaveChangesAsync(ct);

        var seasonPath = $"{rootPath}/{showName}/Season01";
        var seasonCollectionId = (await db.MediaCollections.SingleAsync(c => c.Path == seasonPath, ct)).Id;

        reader.AddEpisodePictureFile(showName, seasonNo: 1, episodeNo: 1, type: "fanart");
        await scanner.ScanMediaCollectionAsync(seasonCollectionId, ct);
        await classifier.ClassifyAllAsync(ct);

        Assert.NotNull(episode.FanartPictureId);
        Assert.True(episode.BackgroundImageRequiresUpdate);
    }

    [Fact]
    public async Task ClassifyAllAsync_DoesNotMarkBackgroundForUpdate_WhenOnlyBannerFileIsAssigned()
    {
        var connectionString = "Data Source=file:classifier-background-banner?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var rootPath = "/media";
        var showName = "TestShow";
        var reader = new SeriesSftpMediaSourceReader(rootPath, showName, seasonCount: 1, episodesPerSeason: 1);

        var serviceProvider = BuildServiceProvider(connectionString, reader);
        await SeedMediaSourceAsync(serviceProvider, rootPath);

        var ct = TestContext.Current.CancellationToken;
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

        await ScanShowAndSeasonAsync(scanner, ct);
        await classifier.ClassifyAllAsync(ct);

        var episode = await db.TVShowEpisodes.SingleAsync(ct);
        Assert.NotNull(episode.PosterPictureId);

        // Reset the flag raised by the initial poster (thumb) assignment so this test only observes
        // the effect of the subsequent, pure banner assignment. Reuse the same ApplicationDbContext
        // (and thus the same tracked episode instance) as the scanner/classifier so the reset is
        // actually visible to the following classification pass instead of a stale in-memory copy.
        episode.BackgroundImageRequiresUpdate = false;
        await db.SaveChangesAsync(ct);

        var seasonPath = $"{rootPath}/{showName}/Season01";
        var seasonCollectionId = (await db.MediaCollections.SingleAsync(c => c.Path == seasonPath, ct)).Id;

        reader.AddEpisodePictureFile(showName, seasonNo: 1, episodeNo: 1, type: "banner");
        await scanner.ScanMediaCollectionAsync(seasonCollectionId, ct);
        await classifier.ClassifyAllAsync(ct);

        Assert.NotNull(episode.BannerPictureId);
        Assert.False(episode.BackgroundImageRequiresUpdate);
    }

    [Fact]
    public async Task ClassifyAllAsync_DoesNotOverwriteManuallyEditedSeriesMetadata()
    {
        var connectionString = "Data Source=file:classifier-manual-metadata?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var rootPath = "/media";
        var showName = "TestShow";
        var reader = new SeriesSftpMediaSourceReader(rootPath, showName, seasonCount: 1, episodesPerSeason: 1);

        var serviceProvider = BuildServiceProvider(connectionString, reader);
        await SeedMediaSourceAsync(serviceProvider, rootPath);

        var ct = TestContext.Current.CancellationToken;
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

        await ScanShowAndSeasonAsync(scanner, ct);
        await classifier.ClassifyAllAsync(ct);

        var show = await db.TVShows.SingleAsync(ct);
        var season = await db.TVShowSeasons.SingleAsync(ct);
        var episode = await db.TVShowEpisodes.SingleAsync(ct);
        show.Name = "Manual Show";
        show.Plot = "Manual show plot";
        show.ReleaseDate = new DateTime(2023, 1, 2);
        show.IsManuallyEdited = true;
        season.Name = "Manual Season";
        season.PremieredAt = new DateTime(2023, 2, 3);
        season.IsManuallyEdited = true;
        episode.Name = "Manual Episode";
        episode.Plot = "Manual episode plot";
        episode.PremieredAt = new DateTime(2023, 3, 4);
        episode.IsManuallyEdited = true;

        foreach (var item in db.MediaItems)
            item.Changed = true;
        foreach (var collection in db.MediaCollections)
            collection.Changed = true;
        await db.SaveChangesAsync(ct);

        await classifier.ClassifyAllAsync(ct);

        Assert.Equal("Manual Show", show.Name);
        Assert.Equal("Manual show plot", show.Plot);
        Assert.Equal(new DateTime(2023, 1, 2), show.ReleaseDate);
        Assert.Equal("Manual Season", season.Name);
        Assert.Equal(new DateTime(2023, 2, 3), season.PremieredAt);
        Assert.Equal("Manual Episode", episode.Name);
        Assert.Equal("Manual episode plot", episode.Plot);
        Assert.Equal(new DateTime(2023, 3, 4), episode.PremieredAt);
    }

    [Fact]
    public async Task ClassifyAllAsync_ReusesManuallyRenamedSeriesSeasonAndEpisode()
    {
        var connectionString = "Data Source=file:classifier-manual-rename?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var rootPath = "/media";
        var showName = "TestShow";
        var reader = new SeriesSftpMediaSourceReader(rootPath, showName, seasonCount: 1, episodesPerSeason: 1);

        var serviceProvider = BuildServiceProvider(connectionString, reader);
        await SeedMediaSourceAsync(serviceProvider, rootPath);

        var ct = TestContext.Current.CancellationToken;
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

        await ScanShowAndSeasonAsync(scanner, ct);
        await classifier.ClassifyAllAsync(ct);

        var show = await db.TVShows.SingleAsync(ct);
        var season = await db.TVShowSeasons.SingleAsync(ct);
        var episode = await db.TVShowEpisodes.SingleAsync(ct);
        var showId = show.Id;
        var seasonId = season.Id;
        var episodeId = episode.Id;

        show.Name = "Manual Show";
        show.IsManuallyEdited = true;
        season.Name = "Manual Season";
        season.IsManuallyEdited = true;
        episode.Name = "Manual Episode";
        episode.IsManuallyEdited = true;

        foreach (var item in db.MediaItems)
            item.Changed = true;
        foreach (var collection in db.MediaCollections)
            collection.Changed = true;
        await db.SaveChangesAsync(ct);

        await classifier.ClassifyAllAsync(ct);

        Assert.Single(await db.TVShows.ToListAsync(ct));
        Assert.Single(await db.TVShowSeasons.ToListAsync(ct));
        Assert.Single(await db.TVShowEpisodes.ToListAsync(ct));
        Assert.Single(await db.TVShowEpisodeMediaItems.ToListAsync(ct));

        Assert.Equal(showId, show.Id);
        Assert.Equal(seasonId, season.Id);
        Assert.Equal(episodeId, episode.Id);
        Assert.Equal("Manual Show", show.Name);
        Assert.Equal("Manual Season", season.Name);
        Assert.Equal("Manual Episode", episode.Name);
    }

    private static async Task ScanShowAndSeasonAsync(MediaSourceScanner scanner, CancellationToken cancellationToken)
    {
        await scanner.ScanAllSourcesAsync(cancellationToken);
        await scanner.ScanNextMediaCollection(cancellationToken); // root -> discovers show folder
        await scanner.ScanNextMediaCollection(cancellationToken); // show folder -> discovers season folder
        await scanner.ScanNextMediaCollection(cancellationToken); // season folder -> discovers episode files
    }

    private static async Task SeedMediaSourceAsync(IServiceProvider serviceProvider, string rootPath)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, GenresChanged = false });
        db.MediaSources.Add(new MediaSource
        {
            Name = "Test Source",
            Path = rootPath,
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static ServiceProvider BuildServiceProvider(string connectionString, SftpMediaSourceReader reader)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)));
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<SftpMediaSourceReader>(reader);
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<MediaSourceScanner>();
        services.AddScoped<RecentEntryService>();
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();
        services.AddScoped<MediaSourceClassifier>();
        services.AddMemoryCache();
        services.AddScoped<EpisodeBackgroundImageGenerator>();
        services.AddScoped<EpisodeBackgroundImageService>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<EpisodeBackgroundImageOptions>>(
            Microsoft.Extensions.Options.Options.Create(new EpisodeBackgroundImageOptions()));
        services.AddSingleton<ILogger<EpisodeBackgroundImageGenerator>>(NullLogger<EpisodeBackgroundImageGenerator>.Instance);
        services.AddSingleton<ILogger<EpisodeBackgroundImageService>>(NullLogger<EpisodeBackgroundImageService>.Instance);
        services.AddSingleton<IAuthService, TestAuthService>();
        services.AddSingleton<ILogger<MediaSourceScanner>>(NullLogger<MediaSourceScanner>.Instance);
        services.AddSingleton<ILogger<MediaSourceClassifier>>(NullLogger<MediaSourceClassifier>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        return services.BuildServiceProvider();
    }
}
