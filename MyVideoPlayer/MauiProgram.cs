using CommunityToolkit.Maui;

namespace MyVideoPlayer
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp(string appPath)
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureVideoPlayerServices(appPath)
                .UseMauiCommunityToolkitMediaElement()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            return builder.Build();
        }
    }
}
