using System;
using System.Linq;
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Scanner;

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
            return services;
        }

    }
}
