using Microsoft.Extensions.Logging;
using VideoPlayer.Helper;
using VideoPlayer.Helper.Navigation;
using VideoPlayer.Navigation;
using VideoPlayer.Services;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels;

namespace VideoPlayer
{
    public static class MauiProgram
    {

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .RegisterStatusManager()
                .RegisterViewModels()
                .RegisterMediaLibrary()
                .RegisterSecrets()
                .RegisterNavigationManager<NavigationManager>();

            #if DEBUG
            builder.Logging.AddDebug();
            #endif

            return builder.Build();
        }

    }
}
