using System;
using System.Linq;

namespace MyVideoPlayer.ViewModels.Menu
{
    public class SourceMenuViewModel: MenuViewModel
    {

        public const string CommandName_ConfigSource = "configSource";
        public const string CommandName_Rescan = "rescan";
        public const string CommandName_Remove = "remove";

        public SourceMenuViewModel()
            : base()
        {
            Add(new MenuAction() { Title = "Neu laden", CommandParameter = CommandName_Rescan });
            Add(new MenuAction() { Title = "Konfigurieren", CommandParameter = CommandName_ConfigSource });
            Add(new MenuAction() { Title = "Entfernen", CommandParameter = CommandName_Remove });
        }

    }

}
