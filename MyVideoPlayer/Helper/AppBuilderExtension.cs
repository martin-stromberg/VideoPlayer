using MyVideoPlayer.ViewModels;
using MyVideoPlayer.Helper.Navigation;
using VideoPlayerLib.Services.Database;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.Samba;
using MyVideoPlayer.Helper.LibraryScan;
using MyVideoPlayer.Helper.Download;
using Microsoft.Extensions.Logging;
using VideoPlayerLib.Services.Log;

namespace MyVideoPlayer
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
            services.ConfigureViewModels();
            services.AddTransient<ILogger, Logger>();
            services.AddTransient<MediaLibraryDatabaseSettings>();
            services.AddTransient<LibraryScannerSettings>();
            services.AddTransient<LibraryDownloaderSettings>();
            services.AddTransient<SambaShare>();
            services.AddTransient<SambaShareScanner>();

            services.AddSingleton<IRessourceLocation>(sp => new RessourceLocation()
            {
                Path = appPath
            });
            services.AddSingleton<INavigationManager, NavigationManager>();
            services.AddSingleton<IMediaLibraryDatabase, MediaLibraryDatabase>();
            services.AddSingleton<IMediaLibrary, MediaLibrary>();
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
                .Where(t => t.IsAssignableTo(baseType) && t != baseType);
            foreach (var vm in viewModels)
                services.AddTransient(vm);
            return services;
        }
    }
}
