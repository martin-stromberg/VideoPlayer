using Foundation;
using System.Diagnostics;

namespace MyVideoPlayer
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp()
        {
            NSBundle mainBundle = NSBundle.MainBundle;
            string resourcesPath = mainBundle.ResourcePath;
            return MauiProgram.CreateMauiApp(resourcesPath);
        }
    }
}
