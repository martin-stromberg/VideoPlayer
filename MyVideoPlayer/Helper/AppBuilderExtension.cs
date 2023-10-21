using Microsoft.Extensions.Logging;
using MyVideoPlayer.Helper.Download;
using MyVideoPlayer.Helper.Export;
using MyVideoPlayer.Helper.LibraryScan;
using MyVideoPlayer.Helper.Navigation;
using MyVideoPlayer.ViewModels;
using VideoPlayerLib.Services.Database;
using VideoPlayerLib.Services.Log;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.Samba;

namespace MyVideoPlayer.Helper
{
    internal static class AppBuilderExtension
    {

        public static MauiAppBuilder ConfigureVideoPlayerServices(this MauiAppBuilder builder, string appPath)
        {
            builder.Services.ConfigureVideoMeisterServices(appPath);
            return builder;
        }

        public static IServiceCollection ConfigureVideoMeisterServices(this IServiceCollection services, string appPath)
        {
            services.AddLogging();
            services.ConfigureViewModels();
            services.AddSingleton<ILoggerFactory>(sp =>
            {
                var factory = new LoggerFactory();
                factory.AddProvider(new DatabaseLoggerProvider(sp));
                return factory;
            });

            // services.AddTransient<ILogger>( sp => sp.GetService<ILoggerFactory>().CreateLogger());
#if WINDOWS10_0_17763_0_OR_GREATER
#elif ANDROID
#elif IOS
            services.AddSingleton<IShare, MyVideoPlayer.Platforms.iOS.OwnShareImplementation>();
#elif MACCATALYST
#else
#endif

            services.AddTransient<MediaLibraryDatabaseSettings>();
            services.AddTransient<LibraryScannerSettings>();
            services.AddTransient<LibraryDownloaderSettings>();
            services.AddTransient<SambaShare>();
            services.AddTransient<SambaShareScanner>();
            services.AddTransient<UserSecrets>();
            services.AddTransient<IDatabaseExporter, DatabaseExporter>();
            services.AddTransient<ILibraryCleanup, LibraryCleanup>();

            services.AddSingleton<IRessourceLocation>(sp => new RessourceLocation() { Path = appPath });
            services.AddSingleton<INavigationManager, NavigationManager>();
            services.AddSingleton<ILogDatabase>(sp => sp.GetService<IMediaLibraryDatabase>() as ILogDatabase);
            services.AddSingleton<IMediaLibraryDatabase, MediaLibraryDatabase>();
            services.AddSingleton<IMediaLibrary>(sp =>
                                                 new MediaLibrary(sp.GetService<IMediaLibraryDatabase>(), sp.GetService<LibraryScannerSettings>()
                                                                                                            .CacheRootPath));
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddSingleton<ILibraryDownloader, LibraryDownloader>();
            services.AddSingleton<ILibraryCollector, LibraryCollector>();
            return services;
        }

        public static IServiceCollection ConfigureViewModels(this IServiceCollection services)
        {
            var baseType = typeof(BaseViewModel);
            var viewModels = baseType
                .Assembly
                .GetTypes()
                .Where(t => t.IsAssignableTo(baseType) && (t != baseType));
            foreach (var vm in viewModels)
                services.AddTransient(vm);
            return services;
        }

    }
}
