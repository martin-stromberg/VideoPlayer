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
            Element.PropertyChanged += Element_PropertyChanged;
            Tapped = new Command((arg) => {
                if (!bool.TryParse(arg?.ToString(), out var boolArg))
                    boolArg = false;
                ExecuteTapped(boolArg); 
            });
        }

        private void Element_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ElementPropertyChanged(e);
        }
        protected virtual void ElementPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {

        }

        public long Id { get; }
        public BaseServiceModel Element { get; }
        public bool AllowAutoPlay { get; set; }
        public Command Tapped { get; }
        public bool HasCounter { get => GetProperty<bool>(); set => SetProperty(value); }
        public int Counter { get => GetProperty<int>(); set { SetProperty(value); HasCounter = value != 0; } }
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
