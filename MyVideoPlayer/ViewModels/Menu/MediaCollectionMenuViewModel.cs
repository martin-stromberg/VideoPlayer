namespace MyVideoPlayer.ViewModels.Menu
{
    public class MediaCollectionMenuViewModel: MenuViewModel
    {

        public const string CommandName_Rescan = "rescanCollection";

        public MediaCollectionMenuViewModel()
            : base()
        {
            Add(new MenuAction() { Title = "Neu laden", CommandParameter = CommandName_Rescan });
        }

    }

}
