using CommunityToolkit.Maui.Views;

namespace VideoMeister.Helper
{
    public class MediaSourceEventArgs: EventArgs
    {
        public MediaSourceEventArgs(MediaSource source) 
            :base()
        {
            Source = source;
        }

        public MediaSource Source { get; }
    }
}
