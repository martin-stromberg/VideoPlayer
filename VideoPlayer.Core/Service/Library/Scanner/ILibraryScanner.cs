using VideoPlayer.Service.BaseServices;

namespace VideoPlayer.Service.Library.Scanner
{
    public interface ILibraryScanner: ITimerService { }

    public interface ILibraryScannerSettings
    {

        public TimeSpan SourceScanInterval { get; set; }

        public TimeSpan FirstCheck { get; set; }

        public TimeSpan CheckInterval { get; set; }

    }

    public class LibraryScannerSettings: ILibraryScannerSettings
    {

        public TimeSpan SourceScanInterval { get; set; } = TimeSpan.FromHours(24);

        public TimeSpan FirstCheck { get; set; } = TimeSpan.FromSeconds(10);

        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    }
}
