using VideoMeister.ViewModels.Navigation;

namespace VideoMeister.Helper
{
    public class NavigationEventArgs: EventArgs
    {
        public NavigationEventArgs(NavigationContentViewModel contentViewModel)
        {
            ContentViewModel = contentViewModel;
        }

        public NavigationContentViewModel ContentViewModel { get; }
    }
}
