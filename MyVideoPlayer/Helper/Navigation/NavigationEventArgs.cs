using MyVideoPlayer.ViewModels.Navigation;

namespace MyVideoPlayer.Helper.Navigation
{
    public class NavigationEventArgs : EventArgs
    {
        public NavigationEventArgs(NavigationContentViewModel contentViewModel)
        {
            ContentViewModel = contentViewModel;
        }

        public NavigationContentViewModel ContentViewModel { get; }
    }
}
