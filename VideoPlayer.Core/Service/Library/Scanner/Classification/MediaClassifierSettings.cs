namespace VideoPlayer.Service.Library.Scanner.Classification
{
    public class MediaClassifierSettings : IMediaClassifierSettings
    {

        public TimeSpan FirstCheck { get; set; } = TimeSpan.FromSeconds(60);

        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);

    }

}
