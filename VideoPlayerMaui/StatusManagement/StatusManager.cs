using System;
using System.ComponentModel;
using System.Linq;

namespace VideoPlayer.StatusManagement
{

    public class StatusManager: IStatusPublisher, IStatusSubscriber
    {

        public StatusManager() { }

        #region IStatusPublisher
        public void AddStatus(string message)
        {
            LastStatusMessage = message;
        }
        #endregion

        #region IStatusSubscriber
        public event EventHandler<StatusEventArgs> StatusChanged;

        protected virtual void OnStatusChanged(StatusEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        protected virtual void OnStatusChanged(string message)
        {
            OnStatusChanged(new StatusEventArgs(message));
        }
        #endregion

        private string _LastStatusMessage = string.Empty;

        protected string LastStatusMessage
        {
            get
            {
                return _LastStatusMessage;
            }
            set
            {
                _LastStatusMessage = value;
                StartNotifier();
            }
        }

        #region Asynchrone Benachrichtigung
        private DateTime _LastNotification = DateTime.MinValue;
        private BackgroundWorker _Notifier = null;
        private TimeSpan _NotificationIntervall = TimeSpan.FromSeconds(2);
        private string lastNotificationMessage = string.Empty;

        private void StartNotifier()
        {
            if (_Notifier != null)
                return;
            _Notifier = new BackgroundWorker();
            _Notifier.DoWork += CheckAndDoNotification;
            _Notifier.RunWorkerCompleted += NotificationCheckCompleted;
            _Notifier.RunWorkerAsync();
        }

        private void CheckAndDoNotification(object sender, DoWorkEventArgs e)
        {
            if (LastStatusMessage.Equals(lastNotificationMessage))
                return;
            lastNotificationMessage = LastStatusMessage;
            MainThread.InvokeOnMainThreadAsync(() => { OnStatusChanged(lastNotificationMessage); });
        }

        private async void NotificationCheckCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            await Task.Delay(_NotificationIntervall);
            _Notifier.RunWorkerAsync();
        }
        #endregion

    }
}
