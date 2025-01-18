namespace VideoPlayer.Service.Library.Scanner
{
    public interface ILibraryScannerSettings
    {
        public TimeSpan SourceScanInterval { get; set; }

        public TimeSpan FirstCheck { get; set; }

        public TimeSpan CheckInterval { get; set; }
    }
}
