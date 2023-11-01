using System;

/* Nicht gemergte Änderung aus Projekt "VideoPlayerMaui (net8.0-android)"
Vor:
using System.Linq;
Nach:
using System.Linq;
using VideoPlayer;
using VideoPlayer.Services;
using VideoPlayer.Services;
using VideoPlayer.Services.MediaLibrary;
*/
using System.Linq;
using VideoPlayer.Services.Database;
using VideoPlayer.Services.Export;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Classification;
using VideoPlayer.Services.MediaLibrary.Demo;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.MediaLibrary.Maintenance;
using VideoPlayer.Services.MediaLibrary.PlaybackHistory;
using VideoPlayer.Services.MediaLibrary.Scanner;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;

namespace VideoPlayer.Services
{
    public static class ServiceExtension
    {

        public static MauiAppBuilder RegisterMediaLibrary(this MauiAppBuilder builder, string resourcesPath)
        {
            builder.Services.RegisterMediaLibrary(resourcesPath);
            return builder;
        }

        public static IServiceCollection RegisterMediaLibrary(this IServiceCollection services, string resourcesPath)
        {
            services.AddTransient<MediaLibraryDatabaseSettings>();
            services.AddTransient<MediaLibraryEnvironment>(sp => new MediaLibraryEnvironment(resourcesPath));
            services.AddSingleton<IMediaLibraryDatabase, MediaLibraryDatabase>();
            services.AddSingleton<ILogDatabase>(sp => sp.GetService<IMediaLibraryDatabase>() as ILogDatabase);
            services.AddSingleton<ISettingsDataSource>(sp =>
                                                       sp.GetService<IMediaLibraryDatabase>() as ISettingsDataSource);
            services.AddSingleton<IMediaLibrary, MediaLibrary.MediaLibrary>();
            services.AddTransient<DemoLibrary>();
            services.AddTransient<IDatabaseExporter, DatabaseExporter>();
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddSingleton<IMediaItemClassifier, MediaItemClassifier>();
            services.AddTransient<IMediaDownloader, MediaDownloader>();
            services.AddTransient<IDataCleaner, DataCleaner>();
            services.AddSingleton<IPlaybackHistoryManager, PlaybackHistoryManager>();
            services.AddSingleton<IPlaylistManager, PlaylistManager>();
            services.AddSingleton<ISettingsService, SettingsService>();
            return services;
        }

    }
}
