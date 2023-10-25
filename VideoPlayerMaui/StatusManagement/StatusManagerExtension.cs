using System;
using System.Linq;

namespace VideoPlayer.StatusManagement
{
    public static  class StatusManagerExtension
    {

        public static MauiAppBuilder RegisterStatusManager(this MauiAppBuilder builder)
        {
            builder.Services.RegisterStatusManager();
            return builder;
        }

        public static IServiceCollection RegisterStatusManager(this IServiceCollection services)
        {
            services.AddSingleton<IStatusSubscriber, StatusManager>();
            services.AddSingleton<IStatusPublisher>(sp =>
                                                    sp.GetService<IStatusSubscriber>() as IStatusPublisher ?? new StatusManager());
            return services;
        }

    }
}
