using System.Diagnostics;
using UIKit;
using VideoPlayer.Service.Download;

namespace VideoPlayer.Platforms.iOS
{
    public class Program
    {

        // This is the main entry point of the application.
        private static void Main(string[] args)
        {
            // if you want to use a different Application Delegate class from "AppDelegate"
            // you can specify it here.
            try
            {
                UIApplication.Main(args, null, typeof(AppDelegate));
            }
            catch (Exception ex)
            {
                LogError(ex);
                Debug.WriteLine($"!!! AUSNAHMEFEHLER: {ex}");
            }
        }

        private static void LogError(Exception ex)
        {
            try
            {
                var environment = new ApplicationEnvironment();
                string logPath = Path.Combine(environment.GetErrorLogPath(), $"{Guid.NewGuid()}.error");
                File.WriteAllText(logPath, $"{DateTime.Now}\r\n{ex}");
            }
            catch { }
        }
    }
}
