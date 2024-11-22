using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using VideoPlayer.Service.Events;

namespace VideoPlayer.ViewModels
{
    public  interface IAppearable
    {
        void ExecuteAppeared();
        void ExecuteDisappeared();
    }
    public interface INavigatable
    {
        void ExecuteNavigatingFrom();
        void ExecuteNavigatedFrom();
        void ExecuteNavigatedTo();

    }

    public class BaseViewModelEventArgs : EventArgs
    {
        public BaseViewModelEventArgs(BaseViewModel viewModel)
            :base()
        {
            ViewModel = viewModel;
        }

        public BaseViewModel ViewModel { get; }
    }
    public class BaseViewModel: IEventSubscriber, 
        INotifyPropertyChanged,
        IAppearable,
        INavigatable
    {

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

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private ConcurrentDictionary<string, object> _Properties = new ConcurrentDictionary<string, object>();

        protected T GetProperty<T>([CallerMemberName] string name = "")
        {
            if (!_Properties.ContainsKey(name))
                return default(T);
            return (T)_Properties[name];
        }

        protected void SetProperty<T>(T value, [CallerMemberName] string name = "")
        {
            SetProperty((object)value, name);
        }

        protected void SetProperty(object value, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(nameof(name));
            _Properties.AddOrUpdate(name, value, (name, oldValue) => value);
            OnPropertyChanged(name);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region IAppearable
        private bool firstAppeared = true;
        protected bool IsAppeared { get; set; }
        public virtual void ExecuteAppeared() 
        {
            IsAppeared = true;
            if (firstAppeared)
            {
                firstAppeared = false;
                ExecuteFirstAppeared();
            }
        }
        public virtual void ExecuteDisappeared() { IsAppeared = false; }
        protected virtual void ExecuteFirstAppeared()
        {

        }
        public virtual void ExecuteNavigatingFrom()
        {
            IsAppeared = false;
        }
        public virtual void ExecuteNavigatedFrom()
        {

        }
        public virtual void ExecuteNavigatedTo()
        {

        }
        #endregion

        public void ProcessNotification(object sender, NotificationEventArgs e)
        {
            switch (e.Name)
            {
                case "StatusUpdated":
                    OnStatusReceived((string)e.Data);
                    break;
                case "Error":
                    OnStatusReceived(((Exception)e.Data).Message);
                    break;
            }
        }

        protected virtual void OnStatusReceived(string statusMessage) { }

    }
}
