using Foundation;

namespace Mediathek.Platforms.MacCatalyst
{
    [Register("AppDelegate")]
    public class AppDelegate: MauiUIApplicationDelegate
    {

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp(string.Empty);

    }
}
