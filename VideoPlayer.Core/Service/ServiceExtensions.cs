using System;
using System.Linq;
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Device;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.ErrorHandling;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Export;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Scanner;
using VideoPlayer.Service.Library.Scanner.Classification;
using VideoPlayer.Service.Library.Scanner.Picture;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Status;

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
            services.AddSingleton<IMediaLibrary, MediaLibrary>();
            services.AddTransient<IDatabaseSettings, DatabaseSettings>();
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddSingleton<IDeviceDisplayManager, DeviceDisplayManager>();
            services.AddSingleton<IMediaPictureProcessor, MediaPictureProcessor>();
            services.AddSingleton<IMediaClassifier, MediaClassifier>();
            services.AddTransient<IMediaClassifierSettings, MediaClassifierSettings>();
            services.AddTransient<ILibraryScannerSettings, LibraryScannerSettings>();
            services.AddTransient<IDataExporter, DataExporter>();
            services.AddSingleton<IPlaylistManager, PlaylistManager>();
            services.AddSingleton<IDownloadManager, DownloadManager>();
            services.AddTransient<IEnvironment, ApplicationEnvironment>();
            services.AddTransient<IMediaCollectionSelector, MediaCollectionSelector>();
            services.AddSingleton<IStatusManager, StatusManager>();
            services.AddSingleton<IMemoryInformation, MemoryInformation>();
            services.AddTransient<IErrorLogManager, ErrorLogManager>();            
            return services;
        }

    }
}
