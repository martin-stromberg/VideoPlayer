using System;
using System.ComponentModel;
using System.Linq;

namespace Mediathek.StatusManagement
{

    public class StatusManager: IStatusPublisher, IStatusSubscriber
    {

        public StatusManager() { }

        #region IStatusPublisher
        public long AddStatus(string message, bool direct)
        {
            LastStatusMessage = message;
            _LastStatusId = DateTime.Now.Ticks;
            if (direct)
                CheckAndDoNotification(this, DoWorkEventArgs.Empty as DoWorkEventArgs);
            return _LastStatusId;
        }

        public void Clear(long id)
        {
            if (_LastStatusId == id)
                AddStatus(string.Empty, true);
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
        private long _LastStatusId = 0;

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
            try
            {
                MainThread.InvokeOnMainThreadAsync(() => { OnStatusChanged(lastNotificationMessage); });
            }
            catch (InvalidOperationException) { }
        }

        private async void NotificationCheckCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            await Task.Delay(_NotificationIntervall);
            _Notifier.RunWorkerAsync();
        }
        #endregion

    }
}
