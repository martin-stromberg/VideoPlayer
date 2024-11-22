namespace VideoPlayer.ViewModels
{
    public class ViewModelEventArgs: EventArgs
    {
        public ViewModelEventArgs(BaseViewModel viewModel)
            :base()
        {
            ViewModel = viewModel;
        }

        public BaseViewModel ViewModel { get; }
    }
}
