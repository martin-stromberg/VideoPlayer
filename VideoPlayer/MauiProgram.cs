using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using VideoPlayer.Navigation;
using VideoPlayer.Service;
using VideoPlayer.Service.Log;
using VideoPlayer.Services;
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
                .UseMauiCommunityToolkitMediaElement()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .RegisterServices()
                .RegisterNavigation()
                .RegisterLocalServices()
                .RegisterViewModels();

            #if DEBUG
            builder.Logging.AddDebug()
                .AddFilter("VideoPlayer", LogLevel.Trace)
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddProvider(new DatabaseLoggerProvider());
#endif


            return builder.Build();
        }

    }
}
