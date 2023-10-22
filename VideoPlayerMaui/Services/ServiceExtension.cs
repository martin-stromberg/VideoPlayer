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
using VideoPlayer.Services.MediaLibrary.Demo;
using VideoPlayer.Services.MediaLibrary.Scanner;

namespace VideoPlayer.Services
{
    public static class ServiceExtension
    {

        public static MauiAppBuilder RegisterMediaLibrary(this MauiAppBuilder builder)
        {
            builder.Services.RegisterMediaLibrary();
            return builder;
        }

        public static IServiceCollection RegisterMediaLibrary(this IServiceCollection services)
        {
            services.AddTransient<MediaLibraryDatabaseSettings>();
            services.AddTransient<MediaLibrarySettings>();
            services.AddSingleton<IMediaLibraryDatabase, MediaLibraryDatabase>();
            services.AddTransient<IMediaLibrary, MediaLibrary.MediaLibrary>();
            services.AddTransient<DemoLibrary>();
            services.AddTransient<IDatabaseExporter, DatabaseExporter>();
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            return services;
        }

    }
}
