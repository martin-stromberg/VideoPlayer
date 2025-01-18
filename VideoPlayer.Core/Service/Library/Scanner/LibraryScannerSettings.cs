namespace VideoPlayer.Service.Library.Scanner
{
    public class LibraryScannerSettings: ILibraryScannerSettings
    {

        public TimeSpan SourceScanInterval { get; set; } = TimeSpan.FromHours(24);

        public TimeSpan FirstCheck { get; set; } = TimeSpan.FromSeconds(10);

        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    }
}
