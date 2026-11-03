using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
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
public class MediaSourceScanServiceTests
{
    [Fact]
    public async Task ExecuteAsync_RunsScanAndClassification_WhenNoCollectionsExist()
    {
        var messages = new ConcurrentQueue<string>();
        var logger = new ListLogger<MediaSourceScanService>(messages);

        var connectionString = "Data Source=file:scan-tests?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var timeProvider = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(11));

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(new SftpMediaSourceReader());
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<MediaSourceScanner>();
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
        services.AddSingleton<ILogger<MediaSourceScanner>>(NullLogger<MediaSourceScanner>.Instance);
        services.AddSingleton<ILogger<MediaSourceClassifier>>(NullLogger<MediaSourceClassifier>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        services.AddSingleton<MediaUpdateNotificationService>(TestableMediaSourceScanService.CreateNullNotificationService());
        services.AddSingleton<IPlaylistBackfillSignal, PlaylistBackfillSignal>();
        services.AddSingleton<IProgramScheduleSignal, ProgramScheduleSignal>();
        services.AddScoped<MediaSourceScanCoordinator>();
        services.AddSingleton<ILogger<MediaSourceScanCoordinator>>(new ListLogger<MediaSourceScanCoordinator>(messages));

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
            db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, GenresChanged = false });
            db.SaveChanges();
        }

        var service = new TestableMediaSourceScanService(
            serviceProvider,
            serviceProvider.GetRequiredService<EventManager>(),
            logger,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(10),
            skipUpgrade: true,
            timeProvider);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);

        await TestHelpers.WaitForMessageAsync(messages, "Starte Scan aller Quellen.", TimeSpan.FromSeconds(2));
        await TestHelpers.WaitForMessageAsync(messages, "Scan aller Quellen abgeschlossen.", TimeSpan.FromSeconds(2));
        await TestHelpers.WaitForMessageAsync(messages, "Klassifizierung abgeschlossen.", TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task ExecuteAsync_AddsMediaItem_WhenSourceContainsMovieFile()
    {
        var messages = new ConcurrentQueue<string>();
        var logger = new ListLogger<MediaSourceScanService>(messages);

        var connectionString = "Data Source=file:scan-tests-movie?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var rootPath = "/movies";
        var fileName = "movie.mp4";
        var filePath = $"{rootPath}/{fileName}";

        var timeProvider = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(11));

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(new FakeSftpMediaSourceReader(rootPath, fileName));
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<MediaSourceScanner>();
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
        services.AddSingleton<ILogger<MediaSourceScanner>>(NullLogger<MediaSourceScanner>.Instance);
        services.AddSingleton<ILogger<MediaSourceClassifier>>(NullLogger<MediaSourceClassifier>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        services.AddSingleton<MediaUpdateNotificationService>(TestableMediaSourceScanService.CreateNullNotificationService());
        services.AddSingleton<IPlaylistBackfillSignal, PlaylistBackfillSignal>();
        services.AddSingleton<IProgramScheduleSignal, ProgramScheduleSignal>();
        services.AddScoped<MediaSourceScanCoordinator>();
        services.AddSingleton<ILogger<MediaSourceScanCoordinator>>(new ListLogger<MediaSourceScanCoordinator>(messages));

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
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
            db.SaveChanges();
        }

        var service = new TestableMediaSourceScanService(
            serviceProvider,
            serviceProvider.GetRequiredService<EventManager>(),
            logger,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(10),
            skipUpgrade: true,
            timeProvider);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);

        await TestHelpers.WaitForMediaItemAsync(serviceProvider, filePath, TimeSpan.FromSeconds(2));
        await TestHelpers.WaitForMediaItemClassifiedAsync(serviceProvider, filePath, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask;

        Assert.DoesNotContain(messages, message => message.Contains("Fehler im MediaSourceScanService"));
    }

    [Fact]
    public async Task ExecuteAsync_ClassifiesEpisodes_WhenSeriesStructureExists()
    {
        var messages = new ConcurrentQueue<string>();
        var logger = new ListLogger<MediaSourceScanService>(messages);

        var connectionString = "Data Source=file:scan-tests-series?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var rootPath = "/media";
        var showName = "TestShow";
        var seasons = 5;
        var episodesPerSeason = 5;
        var firstEpisodePath = $"{rootPath}/{showName}/Season01/S01E01.mp4";
        var expectedEpisodeCount = seasons * episodesPerSeason;

        var timeProvider = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(11));

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(
            new SeriesSftpMediaSourceReader(rootPath, showName, seasons, episodesPerSeason));
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<MediaSourceScanner>();
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
        services.AddSingleton<ILogger<MediaSourceScanner>>(NullLogger<MediaSourceScanner>.Instance);
        services.AddSingleton<ILogger<MediaSourceClassifier>>(NullLogger<MediaSourceClassifier>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        services.AddSingleton<MediaUpdateNotificationService>(TestableMediaSourceScanService.CreateNullNotificationService());
        services.AddSingleton<IPlaylistBackfillSignal, PlaylistBackfillSignal>();
        services.AddSingleton<IProgramScheduleSignal, ProgramScheduleSignal>();
        services.AddScoped<MediaSourceScanCoordinator>();
        services.AddSingleton<ILogger<MediaSourceScanCoordinator>>(new ListLogger<MediaSourceScanCoordinator>(messages));

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
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
            db.SaveChanges();
        }

        var service = new TestableMediaSourceScanService(
            serviceProvider,
            serviceProvider.GetRequiredService<EventManager>(),
            logger,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(10),
            skipUpgrade: true,
            timeProvider);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);

        await TestHelpers.WaitForMediaItemAsync(serviceProvider, firstEpisodePath, TimeSpan.FromSeconds(5));
        await TestHelpers.WaitForMediaItemClassifiedAsync(serviceProvider, firstEpisodePath, TimeSpan.FromSeconds(5));
        await TestHelpers.WaitForTvShowEpisodeCountAsync(serviceProvider, expectedEpisodeCount, TimeSpan.FromSeconds(5));
        await TestHelpers.WaitForMessageAsync(messages, "Klassifizierung abgeschlossen.", TimeSpan.FromSeconds(5));

        cts.Cancel();
        await runTask;

        var ct = TestContext.Current.CancellationToken;
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mediaItemCount = await db.MediaItems.CountAsync(ct);
            // +2 for show-level files (tvshow.nfo + poster.jpg) created by `SeriesSftpMediaSourceReader`
            Assert.Equal(seasons * episodesPerSeason * 3 + 2, mediaItemCount);
            Assert.Equal(seasons, await db.TVShowSeasons.CountAsync(ct));
            Assert.Equal(expectedEpisodeCount, await db.TVShowEpisodes.CountAsync(ct));
        }

        Assert.Contains(messages, message => message.Contains("Klassifizierung abgeschlossen."));
    }

    [Fact]
    public async Task ExecuteAsync_DetectsNewSeason_WhenEpisodesAddedAfterFirstScan()
    {
        var messages = new ConcurrentQueue<string>();
        var logger = new ListLogger<MediaSourceScanService>(messages);

        var connectionString = "Data Source=file:scan-tests-series-update?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var rootPath = "/media";
        var showName = "TestShow";
        var seasons = 5;
        var episodesPerSeason = 5;
        var expectedEpisodeCount = seasons * episodesPerSeason;
        var initialMediaItemCount = seasons * episodesPerSeason * 3 + 2;

        var timeProvider = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        var reader = new SeriesSftpMediaSourceReader(rootPath, showName, seasons, episodesPerSeason);
        var waitTimeSec = 10;

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(reader);
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<MediaSourceScanner>();
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
        services.AddSingleton<ILogger<MediaSourceScanner>>(NullLogger<MediaSourceScanner>.Instance);
        services.AddSingleton<ILogger<MediaSourceClassifier>>(NullLogger<MediaSourceClassifier>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        services.AddSingleton<MediaUpdateNotificationService>(TestableMediaSourceScanService.CreateNullNotificationService());
        services.AddSingleton<IPlaylistBackfillSignal, PlaylistBackfillSignal>();
        services.AddSingleton<IProgramScheduleSignal, ProgramScheduleSignal>();
        services.AddScoped<MediaSourceScanCoordinator>();
        services.AddSingleton<ILogger<MediaSourceScanCoordinator>>(new ListLogger<MediaSourceScanCoordinator>(messages));

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
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
            db.SaveChanges();
        }

        var service = new TestableMediaSourceScanService(
            serviceProvider,
            serviceProvider.GetRequiredService<EventManager>(),
            logger,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(10),
            skipUpgrade: true,
            timeProvider);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);

        await TestHelpers.WaitForMessageCountAsync(messages, "Klassifizierung abgeschlossen.", 1, TimeSpan.FromSeconds(waitTimeSec));
        await TestHelpers.WaitForTvShowEpisodeCountAsync(serviceProvider, expectedEpisodeCount, TimeSpan.FromSeconds(waitTimeSec));
        await TestHelpers.WaitForMediaItemCountAsync(serviceProvider, initialMediaItemCount, TimeSpan.FromSeconds(waitTimeSec));

        reader.AddSeason(showName, episodesPerSeason);
        timeProvider.Advance(TimeSpan.FromDays(8));

        // Wait until the new season collection is discovered by the scanner before asserting episode counts
        var newSeasonPath = $"{rootPath}/{showName}/Season{(seasons + 1).ToString("00")}";
        await TestHelpers.WaitForMediaCollectionAsync(serviceProvider, newSeasonPath, TimeSpan.FromSeconds(waitTimeSec));

        // Ensure the new season collection has been scanned and at least one episode file exists (otherwise episode counts can be flaky)
        var newSeasonFirstEpisodePath = $"{newSeasonPath}/S{seasons + 1:00}E01.mp4";
        await TestHelpers.WaitForMediaItemAsync(serviceProvider, newSeasonFirstEpisodePath, TimeSpan.FromSeconds(waitTimeSec));
        await TestHelpers.WaitForMediaItemClassifiedAsync(serviceProvider, newSeasonFirstEpisodePath, TimeSpan.FromSeconds(waitTimeSec));

        // Dump DB state into the test messages to help debugging if counts don't match
        await TestHelpers.DumpDatabaseStateAsync(serviceProvider, messages);

        var updatedEpisodeCount = (seasons + 1) * episodesPerSeason;
        // +2 for show-level files (tvshow.nfo + poster.jpg) created by `SeriesSftpMediaSourceReader`
        var updatedMediaItemCount = (seasons + 1) * episodesPerSeason * 3 + 2;

        await TestHelpers.WaitForMessageCountAsync(messages, "Klassifizierung abgeschlossen.", 2, TimeSpan.FromSeconds(waitTimeSec));
        await TestHelpers.WaitForTvShowEpisodeCountAsync(serviceProvider, updatedEpisodeCount, TimeSpan.FromSeconds(waitTimeSec));
        await TestHelpers.WaitForMediaItemCountAsync(serviceProvider, updatedMediaItemCount, TimeSpan.FromSeconds(waitTimeSec));

        cts.Cancel();
        await runTask;

        Assert.DoesNotContain(messages, message => message.Contains("Fehler im MediaSourceScanService"));
    }

    [Fact]
    public async Task Services_ScannerAndClassifier_IncrementalSeriesProcessing()
    {
        var connectionString = "Data Source=file:scanner-classifier-integration?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var rootPath = "/media";
        var showName = "TestShow";
        var seasons = 5;
        var episodesPerSeason = 5;
        var expectedEpisodeCount = seasons * episodesPerSeason;

        var timeProvider = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var reader = new SeriesSftpMediaSourceReader(rootPath, showName, seasons, episodesPerSeason);

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(reader);
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<MediaSourceScanner>();
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
        services.AddSingleton<ILogger<MediaSourceScanner>>(NullLogger<MediaSourceScanner>.Instance);
        services.AddSingleton<ILogger<MediaSourceClassifier>>(NullLogger<MediaSourceClassifier>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
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
            db.SaveChanges();
        }

        using var runScope = serviceProvider.CreateScope();
        var scanner = runScope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        var classifier = runScope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

        // 1) Scan all sources -> root collection should be added
        var ct = TestContext.Current.CancellationToken;
        await scanner.ScanAllSourcesAsync(ct);
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.MediaCollections.AnyAsync(c => c.Path == rootPath, ct));
        }

        // 2) First incremental scan -> should add the series folder
        var first = await scanner.ScanNextMediaCollection(ct);
        Assert.True(first);
        var showPath = $"{rootPath}/{showName}";
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.MediaCollections.AnyAsync(c => c.Path == showPath, ct));
        }

        // 3) Scan next -> should add season collections for all seasons
        var second = await scanner.ScanNextMediaCollection(ct);
        Assert.True(second);
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // files in show (tvshow.nfo) should have been added when scanning the show            
            Assert.True(await db.MediaItems.AnyAsync(i => i.Path == $"{showPath}/tvshow.nfo", ct));
            Assert.True(await db.MediaItems.AnyAsync(i => i.Path == $"{showPath}/poster.jpg", ct));
            for (int si = 1; si <= seasons; si++)
            {
                var seasonPath = $"{rootPath}/{showName}/Season{si:00}";
                Assert.True(await db.MediaCollections.AnyAsync(c => c.Path == seasonPath, ct));
            }
        }

        // 4) Scan each season - should add media items for each season
        for (int si = 1; si <= seasons; si++)
        {
            var ok = await scanner.ScanNextMediaCollection(ct);
            Assert.True(ok, $"Expected ScanNextMediaCollection to return true for season {si}");
            using (var s = serviceProvider.CreateScope())
            {
                var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var seasonPath = $"{rootPath}/{showName}/Season{si:00}";
                // each season should contain at least one mp4 media item
                Assert.True(await db.MediaItems.AnyAsync(i => i.Path.StartsWith(seasonPath) && i.Path.EndsWith(".mp4"), ct),
                    $"No media items found for {seasonPath}");
                Assert.True(await db.MediaItems.AnyAsync(i => i.Path.StartsWith(seasonPath) && i.Path.EndsWith("-thumb.jpg"), ct),
                    $"No thumb file found for {seasonPath}");
                Assert.True(await db.MediaItems.AnyAsync(i => i.Path.StartsWith(seasonPath) && i.Path.EndsWith(".nfo"), ct),
                    $"No media items found for {seasonPath}");
            }
        }

        // 5) After scanning all seasons, the next scan should return false
        var afterAll = await scanner.ScanNextMediaCollection(ct);
        Assert.False(afterAll);

        // 6) Run classification - this should create TVShows/Seasons/Episodes
        await classifier.ClassifyAllAsync(ct);
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.TVShows.CountAsync(ct));
            Assert.Equal(seasons, await db.TVShowSeasons.CountAsync(ct));
            Assert.Equal(expectedEpisodeCount, await db.TVShowEpisodes.CountAsync(ct));
        }

        // 7) Add sixth season and run ScanAllSourcesAsync - root should be marked for scanning
        reader.AddSeason(showName, episodesPerSeason);
        await scanner.ScanAllSourcesAsync(ct);
        // scanner used the TimeProvider; PeekLastReturnedUtc returns the value used by the last GetUtcNow call
        var scannerNow = timeProvider.PeekLastReturnedUtc().UtcDateTime;
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var root = await db.MediaCollections.FirstOrDefaultAsync(c => c.Path == rootPath, ct);
            var source = await db.MediaSources.FirstOrDefaultAsync(ms => ms.Path == rootPath, ct);
            source = await db.MediaSources.FirstOrDefaultAsync(ct);

            Assert.NotNull(root);
            Assert.NotNull(root!.ScanDueAt);
            // ScanDueAt should equal the time used by the scanner
            Assert.Equal(scannerNow, root.ScanDueAt.Value);

            Assert.NotNull(source);
            Assert.NotNull(source!.LastScannedAt);
            Assert.Equal(scannerNow, source.LastScannedAt.Value);
        }

        // 8) Next incremental scan should pick up the new season (returns true)
        var picked = await scanner.ScanNextMediaCollection(ct);
        Assert.True(picked);

        // After picking, the series MediaCollection should have an updated ScanDueAt (current)
        var pickTime = timeProvider.PeekLastReturnedUtc().UtcDateTime;
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var showCollection = await db.MediaCollections.FirstOrDefaultAsync(c => c.Path == showPath, ct);
            Assert.NotNull(showCollection);
            Assert.NotNull(showCollection!.ScanDueAt);
            Assert.Equal(pickTime, showCollection.ScanDueAt.Value);
        }

        // 9) Next scan should add the sixth season collection
        var addedSeason = await scanner.ScanNextMediaCollection(ct);
        Assert.True(addedSeason);
        var sixthSeasonPath = $"{rootPath}/{showName}/Season{(seasons + 1):00}";
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.MediaCollections.AnyAsync(c => c.Path == sixthSeasonPath, ct));
        }

        // 10) Next scan should add episodes (media items) for the sixth season
        var scannedSeason = await scanner.ScanNextMediaCollection(ct);
        Assert.True(scannedSeason);
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.MediaItems.AnyAsync(i => i.Path.StartsWith(sixthSeasonPath) && i.Path.EndsWith(".mp4"), ct));
            Assert.True(await db.MediaItems.AnyAsync(i => i.Path.StartsWith(sixthSeasonPath) && i.Path.EndsWith(".nfo"), ct));
            Assert.True(await db.MediaItems.AnyAsync(i => i.Path.StartsWith(sixthSeasonPath) && i.Path.EndsWith("-thumb.jpg"), ct));
        }

        // 11) After processing the sixth season, next scan should return false
        var afterSixth = await scanner.ScanNextMediaCollection(ct);
        Assert.False(afterSixth);

        // 12) Classify all again so the newly added season/episodes are processed
        await classifier.ClassifyAllAsync(ct);
        using (var s = serviceProvider.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(seasons + 1, await db.TVShowSeasons.CountAsync(ct));
            Assert.Equal((seasons + 1) * episodesPerSeason, await db.TVShowEpisodes.CountAsync(ct));
        }
    }

    [Fact]
    public async Task Worker_AndManualFullScan_ProduceSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        const string showName = "TestShow";
        const string rootPath = "/media";
        const int seasons = 2;
        const int episodesPerSeason = 3;

        var workerResult = await RunViaWorkerAsync("scan-tests-regression-worker", rootPath, showName, seasons, episodesPerSeason, ct);
        var coordinatorResult = await RunViaCoordinatorAsync("scan-tests-regression-coordinator", rootPath, showName, seasons, episodesPerSeason, ct);

        Assert.Equal(seasons * episodesPerSeason * 3 + 2, workerResult.MediaItemCount);
        Assert.Equal(1, workerResult.TvShowCount);
        Assert.Equal(seasons, workerResult.TvShowSeasonCount);
        Assert.Equal(seasons * episodesPerSeason, workerResult.TvShowEpisodeCount);
        Assert.False(workerResult.GenresChanged);

        Assert.Equal(workerResult.MediaItemCount, coordinatorResult.MediaItemCount);
        Assert.Equal(workerResult.TvShowCount, coordinatorResult.TvShowCount);
        Assert.Equal(workerResult.TvShowSeasonCount, coordinatorResult.TvShowSeasonCount);
        Assert.Equal(workerResult.TvShowEpisodeCount, coordinatorResult.TvShowEpisodeCount);
        Assert.Equal(workerResult.GenresChanged, coordinatorResult.GenresChanged);
        Assert.Equal(workerResult.BackfillWakeCount, coordinatorResult.BackfillWakeCount);
        Assert.Equal(workerResult.ScheduleWakeCount, coordinatorResult.ScheduleWakeCount);
    }

    private static async Task<ScanRunComparisonResult> RunViaWorkerAsync(
        string dbName, string rootPath, string showName, int seasons, int episodesPerSeason, CancellationToken ct)
    {
        var messages = new ConcurrentQueue<string>();
        var logger = new ListLogger<MediaSourceScanService>(messages);

        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var timeProvider = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(11));
        var reader = new SeriesSftpMediaSourceReader(rootPath, showName, seasons, episodesPerSeason);

        var serviceProvider = BuildScanRunServices(timeProvider, connectionString, reader, messages);
        await SeedAsync(serviceProvider, rootPath, ct);

        var backfillSignal = serviceProvider.GetRequiredService<IPlaylistBackfillSignal>();
        var scheduleSignal = serviceProvider.GetRequiredService<IProgramScheduleSignal>();
        await using var backfillWakes = new WakeCounter(backfillSignal.WaitAsync, ct);
        await using var scheduleWakes = new WakeCounter(scheduleSignal.WaitAsync, ct);

        var service = new TestableMediaSourceScanService(
            serviceProvider,
            serviceProvider.GetRequiredService<EventManager>(),
            logger,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(10),
            skipUpgrade: true,
            timeProvider);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);

        // Erst auf das vollständige Ergebnis warten (nicht nur die erste Logmeldung): Bei mehreren
        // Seasons läuft die zweite Klassifizierungsstufe (ClassifyMediaCollectionsAsync) noch, wenn die
        // erste "Klassifizierung abgeschlossen."-Meldung bereits erscheint.
        var expectedEpisodeCount = seasons * episodesPerSeason;
        await TestHelpers.WaitForTvShowEpisodeCountAsync(serviceProvider, expectedEpisodeCount, TimeSpan.FromSeconds(10));
        await TestHelpers.WaitForMessageCountAsync(messages, "Klassifizierung abgeschlossen.", 2, TimeSpan.FromSeconds(10));

        cts.Cancel();
        await runTask;

        return await ReadResultAsync(serviceProvider, backfillWakes, scheduleWakes, ct);
    }

    private static async Task<ScanRunComparisonResult> RunViaCoordinatorAsync(
        string dbName, string rootPath, string showName, int seasons, int episodesPerSeason, CancellationToken ct)
    {
        var messages = new ConcurrentQueue<string>();
        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var timeProvider = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(11));
        var reader = new SeriesSftpMediaSourceReader(rootPath, showName, seasons, episodesPerSeason);

        var serviceProvider = BuildScanRunServices(timeProvider, connectionString, reader, messages);
        await SeedAsync(serviceProvider, rootPath, ct);

        var backfillSignal = serviceProvider.GetRequiredService<IPlaylistBackfillSignal>();
        var scheduleSignal = serviceProvider.GetRequiredService<IProgramScheduleSignal>();
        await using var backfillWakes = new WakeCounter(backfillSignal.WaitAsync, ct);
        await using var scheduleWakes = new WakeCounter(scheduleSignal.WaitAsync, ct);

        using (var scope = serviceProvider.CreateScope())
        {
            var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
            await coordinator.RunFullScanAsync("Automatischer Scanprozess", 64, ct);
        }

        return await ReadResultAsync(serviceProvider, backfillWakes, scheduleWakes, ct);
    }

    private static ServiceProvider BuildScanRunServices(
        TimeProvider timeProvider,
        string connectionString,
        IMediaSourceReader reader,
        ConcurrentQueue<string> messages)
    {
        var services = new ServiceCollection();
        services.AddSingleton(timeProvider);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(reader);
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<MediaSourceScanner>();
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
        services.AddSingleton<ILogger<MediaSourceScanner>>(NullLogger<MediaSourceScanner>.Instance);
        services.AddSingleton<ILogger<MediaSourceClassifier>>(NullLogger<MediaSourceClassifier>.Instance);
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        services.AddSingleton<MediaUpdateNotificationService>(TestableMediaSourceScanService.CreateNullNotificationService());
        services.AddSingleton<IPlaylistBackfillSignal, PlaylistBackfillSignal>();
        services.AddSingleton<IProgramScheduleSignal, ProgramScheduleSignal>();
        services.AddScoped<MediaSourceScanCoordinator>();
        services.AddSingleton<ILogger<MediaSourceScanCoordinator>>(new ListLogger<MediaSourceScanCoordinator>(messages));

        return services.BuildServiceProvider();
    }

    private static async Task SeedAsync(ServiceProvider serviceProvider, string rootPath, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, GenresChanged = true });
        db.MediaSources.Add(new MediaSource
        {
            Name = "Test Source",
            Path = rootPath,
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<ScanRunComparisonResult> ReadResultAsync(
        ServiceProvider serviceProvider, WakeCounter backfillWakes, WakeCounter scheduleWakes, CancellationToken ct)
    {
        // Grace window so the single wake triggered by disposing the coordinator's/worker's
        // scan scope has time to reach the counting readers before they are stopped.
        await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
        await backfillWakes.DisposeAsync();
        await scheduleWakes.DisposeAsync();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new ScanRunComparisonResult(
            await db.MediaItems.CountAsync(ct),
            await db.TVShows.CountAsync(ct),
            await db.TVShowSeasons.CountAsync(ct),
            await db.TVShowEpisodes.CountAsync(ct),
            (await db.Setups.SingleAsync(ct)).GenresChanged,
            backfillWakes.Count,
            scheduleWakes.Count);
    }

    private sealed record ScanRunComparisonResult(
        int MediaItemCount,
        int TvShowCount,
        int TvShowSeasonCount,
        int TvShowEpisodeCount,
        bool GenresChanged,
        int BackfillWakeCount,
        int ScheduleWakeCount);

    /// <summary>
    /// Zählt Wecksignale eines Wecksignal-Kanals, indem es dessen <c>WaitAsync</c> in einer
    /// Schleife im Hintergrund abwartet (Muster <c>MediaDurationProbeWorkerTests</c>).
    /// </summary>
    private sealed class WakeCounter : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly Task _loopTask;
        private int _count;

        public WakeCounter(Func<CancellationToken, ValueTask> waitAsync, CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        await waitAsync(_cts.Token);
                        Interlocked.Increment(ref _count);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        public int Count => Volatile.Read(ref _count);

        public async ValueTask DisposeAsync()
        {
            if (_cts.IsCancellationRequested)
                return;

            await _cts.CancelAsync();
            try { await _loopTask; } catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }
}
