
using System.Collections.Concurrent;
using System.Net;

namespace VideoWebPlayer.Services
{
    public class InternalConnectionService
    {
        private ConcurrentDictionary<string, DateTime> _connectionAttempts = new ConcurrentDictionary<string, DateTime>();
        protected long ConnectionId { get; set; } = DateTime.Now.Ticks;

        public string GetUserAgent()
        {
            return $"VideoWebPlayer/1.0 Instance/{ConnectionId}";
        }

        public bool IsAllowed(IPAddress? remoteIpAddress)
        {
            var key = remoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(key))
                return false;
            if (_connectionAttempts.TryGetValue(key, out var lastAttempt))
            {
                // Check if the last attempt was within the last 5 minutes
                return (DateTime.Now - lastAttempt).TotalMinutes < 5;
            }
            return false;
        }

        public void Allow(IPAddress? remoteIpAddress)
        {
            var key = remoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(key))
                return;
            _connectionAttempts[key] = DateTime.Now;
        }
    }
}
