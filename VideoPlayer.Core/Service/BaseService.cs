using System;
using System.Linq;
using VideoPlayer.Service.Events;

namespace VideoPlayer.Service
{
    public class BaseService: IEventPublisher, IEventSubscriber, IMultiEventCollection
    {

        #region IEventPublisher
        private Timer _StatusTimer = null;
        private string _LastStatus = string.Empty;
        private string _PreviousStatus = string.Empty;

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
                _LastStatus = string.Empty;
                if (string.IsNullOrWhiteSpace(currentStatus) && (_StatusTimer is not null))
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
                ProcessNotification(e);
        }

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

    }
}
