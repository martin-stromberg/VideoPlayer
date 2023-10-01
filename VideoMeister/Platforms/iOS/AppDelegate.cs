using AVFoundation;
using Foundation;
using UIKit;

namespace VideoMeister;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        EnableBackgroundPlayback();
        return base.FinishedLaunching(application, launchOptions);
    }

    private void EnableBackgroundPlayback()
    {
        var currentSession = AVAudioSession.SharedInstance();
        currentSession.SetCategory(AVAudioSessionCategory.Playback);
        currentSession.SetActive(true);
    }
}
