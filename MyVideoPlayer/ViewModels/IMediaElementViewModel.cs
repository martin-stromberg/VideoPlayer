using CommunityToolkit.Maui.Views;

namespace MyVideoPlayer.ViewModels
{
    public interface IMediaElementViewModel
    {
        bool VideoVisible { get; }
        MediaSource VideoSource { get; }

        void MediaEnded();
        void MediaFailed(string errorMessage);
        void MediaOpened();
        void PositionChanged(TimeSpan position);
        void SeekCompleted();
    }
}
