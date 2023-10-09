using System;
using System.Linq;

namespace MyVideoPlayer.ViewModels.Menu
{
    public class MenuAction
    {

        public MenuAction()
        {
            Command = new Command((sender) => { CommandExecuted?.Invoke(sender, new MenuActionEventArgs(this)); });
        }

        public string Title { get; set; }

        public Command Command { get; }

        public string CommandParameter { get; set; }

        public event EventHandler<MenuActionEventArgs> CommandExecuted;

    }

    public class MenuActionEventArgs: EventArgs
    {

        public MenuActionEventArgs(MenuAction action)
        {
            Action = action;
        }

        public MenuAction Action { get; }

    }

    public class MenuViewModel: BaseViewModel
    {

        private List<MenuAction> _actions = new List<MenuAction>();

        public IEnumerable<MenuAction> Actions
        {
            get
            {
                return _actions;
            }
        }

        protected MenuAction Add(MenuAction action)
        {
            action.CommandExecuted += (sender, e) => { CommandExecuted?.Invoke(sender, e); };
            _actions.Add(action);
            return action;
        }

        public event EventHandler<MenuActionEventArgs> CommandExecuted;

    }
}
