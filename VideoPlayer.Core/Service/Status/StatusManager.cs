using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Events;

namespace VideoPlayer.Service.Status
{
    public class StatusManager: BaseService, IStatusManager
    {
        public StatusManager(ILogger<StatusManager> logger)
            :base(logger)
        {

        }
        private struct StatusEntry
        {
            public DateTime DueTime;
            public string Message;
        }
        private ConcurrentDictionary<int, StatusEntry> _StatusMessages = new ConcurrentDictionary<int, StatusEntry>();
        private bool _UpdateStatusRequested = false;
        private Timer _Timer = null;
        protected override void OnStatusReceived(string statusMessage)
        {
            base.OnStatusReceived(statusMessage);
            int threadId = Thread.CurrentThread.ManagedThreadId;
            var statusEntry = new StatusEntry()
            {
                Message = statusMessage,
                DueTime = DateTime.Now.AddSeconds(10)
            };
            if (_StatusMessages.ContainsKey(threadId))
            {
                if (string.IsNullOrWhiteSpace(statusMessage))
                    _StatusMessages.Remove(threadId, out var _);
                else
                    _StatusMessages[threadId] = statusEntry;
            }
            else if (!string.IsNullOrWhiteSpace(statusMessage))
                _StatusMessages.AddOrUpdate(threadId, statusEntry, (id, existing) => statusEntry);
            _UpdateStatusRequested = true;            
            StartTimer();
        }

        private void StartTimer()
        {
            if (_Timer is not null) return;
            _Timer = new Timer((e) => ClearStatus(), null, 1000, 1000);
        }

        private void ClearStatus()
        {
            try
            {
                bool changed = _UpdateStatusRequested;
                foreach (var entry in _StatusMessages.Where(e => e.Value.DueTime < DateTime.Now).ToArray())
                {
                    changed = true;
                    _StatusMessages.Remove(entry.Key, out var _);
                }
                if (changed)
                    OnStatusChanged();
            }
            catch (Exception ex) 
            {
                NotifyError(ex);
            }
        }

        protected virtual void OnStatusChanged()
        {
            Notify(this, new NotificationEventArgs("StatusUpdated", ToString()));
        }

        public override string ToString()
        {
            return string.Join("\r\n", _StatusMessages.Select(e => e.Value.Message));
        }
    }
}
