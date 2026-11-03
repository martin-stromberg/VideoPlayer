using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Services.EpisodeBackgroundImage;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Builds a small service provider around a SQLite test database with everything
/// <see cref="MediaSourceScanCoordinator"/> needs (Scanner, Classifier, Wecksignale, optionales Gate), für
/// Tests von <see cref="MediaSourceScanCoordinator"/>.
/// </summary>
internal static class MediaSourceScanCoordinatorTestServices
{
    /// <summary>
    /// Baut den Service-Provider.
    /// </summary>
    /// <param name="connectionString">Die Shared-Memory-SQLite-Verbindung der Testdatenbank (von der Keeper-Connection offen gehalten).</param>
    /// <param name="messages">Queue, in die die Logmeldungen des Koordinators geschrieben werden.</param>
    /// <param name="gate">Das optionale Gate, oder <c>null</c>, wenn der Lauf ohne Gate erfolgen soll.</param>
    /// <param name="playlistBackfillSignal">Das zu verwendende Wecksignal, oder <c>null</c> für die Standardimplementierung.</param>
    /// <param name="programScheduleSignal">Das zu verwendende Wecksignal, oder <c>null</c> für die Standardimplementierung.</param>
    /// <param name="notificationService">Der zu verwendende Benachrichtigungsdienst, oder <c>null</c> für einen mit Null-Hub.</param>
    /// <param name="writeCoordinator">Der optionale Schreibsperren-Koordinator des Classifiers.</param>
    /// <returns>Der aufgebaute Provider (am Testende disposen).</returns>
    public static ServiceProvider Build(
        string connectionString,
        ConcurrentQueue<string> messages,
        IBackgroundProcessingGate? gate = null,
        IPlaylistBackfillSignal? playlistBackfillSignal = null,
        IProgramScheduleSignal? programScheduleSignal = null,
        MediaUpdateNotificationService? notificationService = null,
        IMediaMetadataWriteCoordinator? writeCoordinator = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance));
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
        services.AddSingleton(notificationService ?? TestableMediaSourceScanService.CreateNullNotificationService());
        if (writeCoordinator is not null)
            services.AddSingleton(writeCoordinator);
        if (playlistBackfillSignal is not null)
            services.AddSingleton(playlistBackfillSignal);
        else
        {
            services.AddSingleton<PlaylistBackfillSignal>();
            services.AddSingleton<IPlaylistBackfillSignal>(sp => sp.GetRequiredService<PlaylistBackfillSignal>());
        }
        if (programScheduleSignal is not null)
            services.AddSingleton(programScheduleSignal);
        else
        {
            services.AddSingleton<ProgramScheduleSignal>();
            services.AddSingleton<IProgramScheduleSignal>(sp => sp.GetRequiredService<ProgramScheduleSignal>());
        }
        if (gate is not null)
            services.AddSingleton(gate);
        services.AddScoped<MediaSourceScanCoordinator>();
        services.AddSingleton<ILogger<MediaSourceScanCoordinator>>(new ListLogger<MediaSourceScanCoordinator>(messages));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Legt den <see cref="Setup"/>-Datensatz und eine lokale Medienquelle an.
    /// </summary>
    /// <param name="serviceProvider">Der aufgebaute Provider.</param>
    /// <param name="rootDirectory">Das Root-Verzeichnis der lokalen Quelle.</param>
    /// <param name="genresChanged">Startwert für <see cref="Setup.GenresChanged"/>.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Id der angelegten Medienquelle.</returns>
    public static async Task<long> SeedAsync(ServiceProvider serviceProvider, string rootDirectory, bool genresChanged, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, GenresChanged = genresChanged });
        var source = new MediaSource
        {
            Name = "Coordinator Test Source",
            Path = rootDirectory,
            SourceType = MediaSourceType.LocalDirectory,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(cancellationToken);
        return source.Id;
    }
}
