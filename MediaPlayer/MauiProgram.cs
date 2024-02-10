using CommunityToolkit.Maui;
using MediaPlayer.Helper;
using MediaPlayer.Helper.Navigation;
using MediaPlayer.Helper.Touch;
using Mediathek.Navigation;
using Mediathek.Services;
using Mediathek.StatusManagement;
using Mediathek.ViewModels;
using Microsoft.Extensions.Logging;

#if IOS
using MediaPlayer.Platforms.iOS;

#endif
#if WINDOWS
using MediaPlayer.Platforms.Windows;
#endif

namespace MediaPlayer
{
    public static class MauiProgram
    {

        public static MauiApp CreateMauiApp(string resourcesPath)
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>()
                   .ConfigureFonts(fonts =>
                   {
                       fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                       fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                   })
                   .UseMauiCommunityToolkitMediaElement()
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