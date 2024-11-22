using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using VideoPlayer.Service.Events;

namespace VideoPlayer.Service.BaseServices
{
    public class BaseService : IEventPublisher, IEventSubscriber, IMultiEventCollection
    {
        public BaseService(ILogger logger)
            :base()
        {
            this.Logger = logger;
        }
        protected void Log(string message, LogLevel level = LogLevel.Information)
        {
            Logger?.Log(level, 0, GetType(), null, (state, ex) => message);
        }
        protected void Log(Exception error)
        {
            Logger?.Log(LogLevel.Error, 0, GetType(), error, (state, ex) => error.ToString());
        }
        public long InstanceId { get; } = DateTime.Now.Ticks;
        #region IEventPublisher
        private Timer _StatusTimer = null;
        private string _LastStatus = string.Empty;
        private DateTime _LastStatusTime;
        private string _PreviousStatus = string.Empty;
        protected readonly ILogger Logger;

        public event EventHandler<NotificationEventArgs> OnEvent;

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

        #region IEventSubscriber
        /// <summary>
        /// Reagiert auf ein Event
        /// </summary>
        /// <param name="sender"> Auslösendes Objekt </param>
        /// <param name="e"> Eventinformationen </param>
        public void ProcessNotification(object sender, NotificationEventArgs e)
        {
            if (sender != this)
                switch (e.Name)
                {
                    case "Status":
                        OnStatusReceived((string)e.Data);
                        break;
                    case "Error":
                        OnStatusReceived(((Exception)e.Data).Message);
                        break;
                    default:
                        ProcessNotification(e);
                        break;
                }
            
        }
        protected virtual void OnStatusReceived(string statusMessage) { }

        /// <summary>
        /// Reagiert auf ein fremdes Event
        /// </summary>
        /// <param name="e"> Eventinformationen </param>
        protected virtual void ProcessNotification(NotificationEventArgs e) { }
        #endregion

        #region IMultiEventCollection
        public virtual IEnumerable<IEventSubscriber> GetSubscribers()
        {
            return new IEventSubscriber[0];
        }

        public virtual IEnumerable<IEventPublisher> GetPublishers()
        {
            return new IEventPublisher[0];
        }
        #endregion

        protected void StartProcess(string statusMessage = "")
        {
            if (!string.IsNullOrWhiteSpace(statusMessage))
                NotifyStatus(statusMessage);
            Notify(this, new NotificationEventArgs("ProcessStarted", statusMessage));
        }

        protected void FinishProcess()
        {
            Notify(this, new NotificationEventArgs("ProcessFinished", null));
        }

    }
}
