namespace MyVideoPlayer.ViewModels
{
    public class ViewModelEventArgs: EventArgs
    {

        public ViewModelEventArgs(BaseViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public BaseViewModel ViewModel { get; set; }

    }
}
