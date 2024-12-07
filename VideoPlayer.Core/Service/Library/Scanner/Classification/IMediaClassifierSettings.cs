namespace VideoPlayer.Service.Library.Scanner.Classification
{
    public interface IMediaClassifierSettings
    {

        public TimeSpan FirstCheck { get; set; }

        public TimeSpan CheckInterval { get; set; }
    }

}
