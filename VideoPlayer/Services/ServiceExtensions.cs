using System;
using System.Linq;
using VideoPlayer.Service.Export;
using VideoPlayer.Service.Resources;
using VideoPlayer.Services.Registrations;
using VideoPlayer.Services.Resources;

namespace VideoPlayer.Services
{
    public static class ServiceExtensions
    {

        public static MauiAppBuilder RegisterLocalServices(this MauiAppBuilder builder)
        {
            builder.Services.RegisterLocalServices();
            return builder;
        }

        public static IServiceCollection RegisterLocalServices(this IServiceCollection services)
        {
            services.AddTransient<IDataExporterRegistration, DataExporterRegistration>();
            services.AddSingleton<IResourceManager, ResourceManager>();
            return services;
        }

    }
}
