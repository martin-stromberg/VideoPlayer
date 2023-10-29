using AVFoundation;
using Foundation;
using UIKit;

namespace VideoPlayer.Platforms.iOS
{
    [Register("AppDelegate")]
    public class AppDelegate: MauiUIApplicationDelegate
    {

        protected override MauiApp CreateMauiApp()
        {
            NSBundle mainBundle = NSBundle.MainBundle;
            string resourcesPath = mainBundle.ResourcePath;
            return MauiProgram.CreateMauiApp(resourcesPath);
        }

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            var audioSession = AVAudioSession.SharedInstance();
            NSError nSError = new NSError();
            audioSession.SetCategory(AVAudioSessionCategory.Playback);
            audioSession.SetActive(true, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out nSError);
            return base.FinishedLaunching(application, launchOptions);
        }

    }
}
