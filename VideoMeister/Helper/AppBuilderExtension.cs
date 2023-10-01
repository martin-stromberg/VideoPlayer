using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services;
using VideoMeister.Services.Database;
using VideoMeister.Services.VideoSources;

namespace VideoMeister.Helper
{
    internal static class AppBuilderExtension
    {
        public static MauiAppBuilder ConfigureVideoMeisterServices(this MauiAppBuilder builder)
        {
            builder.Services.ConfigureVideoMeisterServices();
            return builder;
        }

        public static IServiceCollection ConfigureVideoMeisterServices(this IServiceCollection services)
        {            
            var databaseSettings = new DatabaseSettings();

            services.AddSingleton<VideoSourceManagerSettings>();
            services.AddSingleton<DatabaseSettings>(databaseSettings);
            if (File.Exists(databaseSettings.FilePath))
                services.AddSingleton<VideoSourceManager, VideoSourceManager>();
            else
                services.AddSingleton<VideoSourceManager, DemoVideoSourceManager>();
            services.AddSingleton<MediaLibraryDatabase>();            
            return services;
        }
    }
}
