using System;
using System.Linq;

namespace MyVideoPlayer.ViewModels.Menu
{
    public class SourcesMenuViewModel: MenuViewModel
    {

        public const string CommandName_NewSource = "newSource";

        public SourcesMenuViewModel()
            : base()
        {
            Add(new MenuAction() { Title = "Neue Quelle", CommandParameter = CommandName_NewSource });
        }

    }

}
