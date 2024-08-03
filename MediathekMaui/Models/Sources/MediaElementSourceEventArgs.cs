namespace Mediathek.Models.Sources
{
    public class MediaElementSourceEventArgs : BaseModelEventArgs
    {
        public MediaElementSourceEventArgs(MediaElementSource source) : base(source)
        {
            Source = source;
        }
        public MediaElementSource Source { get; }
    }
}
