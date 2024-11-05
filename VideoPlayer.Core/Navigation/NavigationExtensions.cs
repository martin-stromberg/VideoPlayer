using System;
using System.Linq;

namespace VideoPlayer.Navigation
{
    public static class NavigationExtensions
    {

        public static MauiAppBuilder RegisterNavigation(this MauiAppBuilder builder)
        {
            builder.Services.RegisterNavigation();
            return builder;
        }

        public static IServiceCollection RegisterNavigation(this IServiceCollection services)
        {
            services.AddSingleton<INavigationManager, NavigationManager>();
            return services;
        }

    }
}
