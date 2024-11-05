using System;
using System.Linq;
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Device;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Export;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Scanner;
using VideoPlayer.Service.Library.Scanner.Classification;
using VideoPlayer.Service.Playlists;

namespace VideoPlayer.Service
{
    public static class ServiceExtensions
    {

        public static MauiAppBuilder RegisterServices(this MauiAppBuilder builder)
        {
            builder.Services.RegisterServices();
            return builder;
        }

        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            services.AddSingleton<IEventController, EventController>();
            services.AddSingleton<IApplicationManager, ApplicationManager>();
            services.AddSingleton<IMediaLibraryDatabase, MediaLibraryDatabase>();
            services.AddTransient<IMediaLibrary, MediaLibrary>();
            services.AddTransient<IDatabaseSettings, DatabaseSettings>();
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddSingleton<IDeviceDisplayManager, DeviceDisplayManager>();
            services.AddSingleton<IMediaClassifier, MediaClassifier>();
            services.AddTransient<IMediaClassifierSettings, MediaClassifierSettings>();
            services.AddTransient<ILibraryScannerSettings, LibraryScannerSettings>();
            services.AddTransient<IDataExporter, DataExporter>();
            services.AddSingleton<IPlaylistManager, PlaylistManager>();
            services.AddSingleton<IDownloadManager, DownloadManager>();
            services.AddTransient<IEnvironment, ApplicationEnvironment>();
            services.AddTransient<IMediaCollectionSelector, MediaCollectionSelector>();
            return services;
        }

    }
}
