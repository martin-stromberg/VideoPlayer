using Mediathek.Services.Database;
using Mediathek.Services.Export;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Classification;
using Mediathek.Services.MediaLibrary.Demo;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.MediaLibrary.Maintenance;
using Mediathek.Services.MediaLibrary.OverviewPreparation;
using Mediathek.Services.MediaLibrary.PlaybackHistory;
using Mediathek.Services.MediaLibrary.Scanner;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using System;
using System.Linq;

namespace Mediathek.Services
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
            services.AddTransient<UserSercretRegistrator>();
            services.AddTransient<MediaLibraryDatabaseSettings>();
            services.AddTransient<MediaLibraryEnvironment>(sp => new MediaLibraryEnvironment(resourcesPath));
            services.AddSingleton<IMediaLibraryDatabase, MediaLibraryDatabase>();
            services.AddSingleton<ILogDatabase>(sp => sp.GetService<IMediaLibraryDatabase>() as ILogDatabase);
            services.AddSingleton<ISettingsDataSource>(sp =>
                                                       sp.GetService<IMediaLibraryDatabase>() as ISettingsDataSource);
            services.AddSingleton<IJobDatabase>(sp => sp.GetService<IMediaLibraryDatabase>() as IJobDatabase);
            services.AddSingleton<IMediaLibrary, MediaLibrary.MediaLibrary>();
            services.AddTransient<DemoLibrary>();
            services.AddTransient<IDatabaseExporter, DatabaseExporter>();
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddSingleton<IMediaItemClassifier, MediaItemClassifier>();
            services.AddSingleton<IDownloadManager, DownloadManager>();
            services.AddTransient<IDataCleaner, DataCleaner>();
            services.AddSingleton<IPlaybackHistoryManager, PlaybackHistoryManager>();
            services.AddSingleton<IPlaylistManager, PlaylistManager>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IOverviewManager, OverviewManager>();

            return services;
        }

    }
}
