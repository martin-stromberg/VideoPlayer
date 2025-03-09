using Microsoft.Extensions.Logging;
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
    public class BaseViewModel: 
        IEventSubscriber,
        IEventPublisher,
        INotifyPropertyChanged,
        IAppearable,
        INavigatable
    {
        public BaseViewModel(ILogger logger)
            :base()
        {
            Logger = logger;
        }
        public Guid InstanceId { get; } = Guid.NewGuid();
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
        protected bool IsAppeared { get => GetProperty<bool>(); set => SetProperty(value); }
        protected bool IsAppearing { get => GetProperty<bool>(); set => SetProperty(value); }
        public virtual void ExecuteAppeared() 
        {
            IsAppearing = true;
            try
            {
                IsAppeared = true;
                if (firstAppeared)
                {
                    firstAppeared = false;
                    ExecuteFirstAppeared();
                }
            }
            finally { IsAppearing = false; }
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

        #region IEventPublisher
        private Timer _StatusTimer = null;
        private string _LastStatus = string.Empty;
        private DateTime _LastStatusTime;
        private string _PreviousStatus = string.Empty;
        protected readonly ILogger Logger;

        public event EventHandler<NotificationEventArgs> OnEvent;

        public virtual void Notify(string msgName)
        {
            Notify(this, new NotificationEventArgs(msgName, null));
        }
        public virtual void Notify(object sender, NotificationEventArgs e)
        {
            OnEvent?.Invoke(sender, e);
        }

        public virtual void NotifyError(Exception error)
        {
            Notify(this, new NotificationEventArgs("Error", error));
        }

        public virtual void NotifyStatus(string message, bool direct = false)
        {
            _LastStatus = message;
            _LastStatusTime = DateTime.Now;
            if (direct)
                SendStatus();
            else if (_StatusTimer is null)
                _StatusTimer = new Timer((args) => { SendStatus(); }, null, 1000, 1000);
        }

        private void SendStatus()
        {
            try
            {
                var currentStatus = _LastStatus;
                if (_LastStatusTime.AddSeconds(5) < DateTime.Now)
                    _LastStatus = string.Empty;
                if (string.IsNullOrWhiteSpace(currentStatus) && _StatusTimer is not null)
                {
                    _StatusTimer.Dispose();
                    _StatusTimer = null;
                }
                if (!string.IsNullOrWhiteSpace(currentStatus) || !string.IsNullOrWhiteSpace(_PreviousStatus))
                    Notify(this, new NotificationEventArgs("Status", currentStatus));
                _PreviousStatus = currentStatus;
            }
            catch { }
        }
        #endregion
    }
}
