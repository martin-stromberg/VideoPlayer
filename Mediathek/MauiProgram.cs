using CommunityToolkit.Maui;
using Mediathek.Helper.Navigation;
using Microsoft.Extensions.Logging;
using Mediathek.Helper;
using Mediathek.Navigation;
using Mediathek.Services;
using Mediathek.StatusManagement;
using Mediathek.ViewModels;


#if IOS
using Mediathek.Platforms.iOS;
#endif
#if WINDOWS
using Mediathek.Platforms.Windows;
#endif
namespace Mediathek
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp(string resourcesPath)
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                //.UseMauiCommunityToolkitMediaElement()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureEffects(effects =>
                {
#if IOS
                    effects.Add<TouchRoutingEffect, TouchPlatformEffect>();
#endif
#if WINDOWS
                    effects.Add<TouchRoutingEffect, TouchPlatformEffect>();
#endif
                })
                   .RegisterSecrets()
                   .RegisterStatusManager()
                   .RegisterViewModels()
                   .RegisterMediaLibrary(resourcesPath)
                   .RegisterNavigationManager<NavigationManager>();
#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
