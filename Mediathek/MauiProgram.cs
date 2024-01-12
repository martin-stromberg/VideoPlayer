global using CommunityToolkit.Maui;
global using CommunityToolkit.Maui.Core.Primitives;
global using Mediathek.Helper;
global using Mediathek.Navigation;
global using Mediathek.ViewModels;
global using Mediathek.Views.Categorization;
global using Mediathek.Views.MediaLists;
global using Mediathek.Views.MediaLists.Cards;
global using Mediathek.Views.VideoPlayer;
global using Microsoft.Extensions.Logging;
using Mediathek.Helper.Navigation;
using Mediathek.Services;
using Mediathek.StatusManagement;

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
                .UseMauiCommunityToolkitMediaElement()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureEffects(effects =>
                {
                #if IOS
                    effects.Add<Mediathek.Helper.Touch.TouchRoutingEffect, TouchPlatformEffect>();
                    #endif
                    #if WINDOWS
                    effects.Add<Mediathek.Helper.Touch.TouchRoutingEffect, TouchPlatformEffect>();
                    #endif
                })
                .RegisterStatusManager()
                .RegisterViewModels()
                .RegisterMediaLibrary(resourcesPath)
                .RegisterSecrets()
                .RegisterNavigationManager<NavigationManager>();

            #if DEBUG
            builder.Logging.AddDebug();
            #endif

            return builder.Build();
        }

    }
}
