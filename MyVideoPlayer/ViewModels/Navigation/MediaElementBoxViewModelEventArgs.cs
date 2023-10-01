namespace MyVideoPlayer.ViewModels.Navigation
{
    public class MediaElementBoxViewModelEventArgs: EventArgs
    {
        public MediaElementBoxViewModelEventArgs(BaseMediaElementBoxViewModel viewModel)
            :base()
        {
            ViewModel = viewModel;
        }

        public BaseMediaElementBoxViewModel ViewModel { get; }
    }
}
