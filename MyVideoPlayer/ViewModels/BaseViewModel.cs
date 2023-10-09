using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MyVideoPlayer.ViewModels
{
    public class BaseViewModel: INotifyPropertyChanged
    {

        #region INotifyPropertyChanged
        private Dictionary<string, object> properties = new Dictionary<string, object>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        protected T GetProperty<T>([CallerMemberName] string name = "")
        {
            if (!properties.ContainsKey(name))
                return default(T);
            return (T)properties[name];
        }

        protected void SetProperty<T>(object value, [CallerMemberName] string name = "")
        {
            var oldValue = GetProperty<T>(name);
            if (!properties.ContainsKey(name))
                properties.Add(name, value);
            else
                properties[name] = value;
            OnPropertyChanged(new PropertyChangedEventArgs(name));
        }
        #endregion

        public string Title
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

    }

    public class ViewModelEventArgs: EventArgs
    {

        public ViewModelEventArgs(BaseViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public BaseViewModel ViewModel { get; set; }

    }
}
