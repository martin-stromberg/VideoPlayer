using System;
using System.Linq;

namespace VideoPlayer.Navigation
{
    public static class NavigationManagerExtensions
    {

        public static MauiAppBuilder RegisterNavigationManager<T>(this MauiAppBuilder builder)
            where T: INavigationManager
        {
            builder.Services.RegisterNavigationManager<T>();
            return builder;
        }

        public static IServiceCollection RegisterNavigationManager<T>(this IServiceCollection services)
            where T: INavigationManager
        {
            services.AddSingleton(typeof(INavigationManager), typeof(T));
            return services;
        }

    }
}
