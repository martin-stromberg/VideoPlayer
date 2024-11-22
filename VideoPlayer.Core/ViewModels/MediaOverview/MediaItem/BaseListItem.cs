using Microsoft.Maui.Controls;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    public class ArgumentEventArgs:EventArgs
    {
        public ArgumentEventArgs(object argument)
            :base()
        {
            Argument = argument;
        }

        public object Argument { get; }
    }
    public class BaseListItem: BaseViewModel
    {
        public BaseListItem(BaseServiceModel element)
            :base()
        {
            Id = element is null ? 0 : element.Id;
            Element = element;
            Tapped = new Command((arg) => ExecuteTapped((bool)arg));
        }
        public long Id { get; }
        public BaseServiceModel Element { get; }
        public bool AllowAutoPlay { get; set; }
        public Command Tapped { get; }
        private void ExecuteTapped(bool autoPlay)
        {
            try
            {
                Selected?.Invoke(this, new ArgumentEventArgs(AllowAutoPlay && autoPlay));
            }
            catch { }
        }
        public event EventHandler<ArgumentEventArgs> Selected;
    }
}
