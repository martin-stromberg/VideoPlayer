namespace Mediathek.ViewModels.VideoPlayer
{
    public class TimeSpanEventArgs: EventArgs
    {

        public TimeSpanEventArgs(TimeSpan position)
            : base()
        {
            Position = position;
        }

        public TimeSpan Position { get; }

    }
}
