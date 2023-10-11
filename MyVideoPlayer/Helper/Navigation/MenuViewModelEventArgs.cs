using MyVideoPlayer.ViewModels.Menu;

namespace MyVideoPlayer.Helper.Navigation
{
    public class MenuViewModelEventArgs: EventArgs
    {
        public MenuViewModelEventArgs(MenuViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public MenuViewModel ViewModel { get; }
    }
}
