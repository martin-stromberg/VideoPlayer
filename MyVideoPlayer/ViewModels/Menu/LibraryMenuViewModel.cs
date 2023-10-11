namespace MyVideoPlayer.ViewModels.Menu
{
    public class LibraryMenuViewModel: MenuViewModel
    {

        public const string CommandName_Rescan = "rescanLibrary";

        public LibraryMenuViewModel()
            : base()
        {
            Add(new MenuAction() { Title = "Neu laden", CommandParameter = CommandName_Rescan });
        }

    }

}
