namespace VideoPlayer.ViewModels.Common
{
    public class TimeSpanEventArgs : EventArgs
    {
        public TimeSpanEventArgs(TimeSpan position)
        {
            Position = position;
        }

        public TimeSpan Position { get; }
    }
}
