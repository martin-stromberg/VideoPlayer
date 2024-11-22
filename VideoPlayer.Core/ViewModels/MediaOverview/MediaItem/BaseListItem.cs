using Microsoft.Maui.Controls;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    public class BaseListItem: BaseViewModel
    {
        public BaseListItem(BaseServiceModel element)
            :base()
        {
            Id = element is null ? 0 : element.Id;
            Element = element;
            Tapped = new Command((sender) => ExecuteTapped(sender));
        }
        public long Id { get; }
        public BaseServiceModel Element { get; }

        public Command Tapped { get; }
        private void ExecuteTapped(object sender)
        {
            try
            {
                Selected?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }
        public event EventHandler Selected;
    }
}
