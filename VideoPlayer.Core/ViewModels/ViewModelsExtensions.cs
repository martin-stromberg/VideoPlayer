using System;
using System.Linq;
using VideoPlayer.ViewModels.HomePage;
using VideoPlayer.ViewModels.MediaOverview;
using VideoPlayer.ViewModels.MediaOverview.Genres;
using VideoPlayer.ViewModels.Setup;

namespace VideoPlayer.ViewModels
{
    public static class ViewModelsExtensions
    {

        public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
        {
            builder.Services.RegisterViewModels();
            return builder;
        }

        public static IServiceCollection RegisterViewModels(this IServiceCollection services)
        {
            services.AddSingleton<HomePageViewModel, HomePageViewModel>();
            services.AddTransient<SettingsViewModel, SettingsViewModel>();
            services.AddTransient<MovieOverviewViewModel, MovieOverviewViewModel>();
            services.AddTransient<TVShowOverviewViewModel, TVShowOverviewViewModel>();
            services.AddTransient<GenreSelectionViewModel, GenreSelectionViewModel>();
            return services;
        }

    }
}
