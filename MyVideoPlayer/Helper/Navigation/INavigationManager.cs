using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.Helper.Navigation
{
    public interface INavigationManager
    {
        event EventHandler<NavigationEventArgs> NavigationCompleted;
        event EventHandler<MediaSourceEventArgs> MediaSourceToPlay;
        event EventHandler<CallbackBaseModelEventArgs> DownloadRequested;

        void NavigateBack();
        void NavigateToLog();
        void NavigateToOverview();
        void NavigateToSourceOverview();
        void VideoClosed(CommunityToolkit.Maui.Views.MediaSource e);
    }
}