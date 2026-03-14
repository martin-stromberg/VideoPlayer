
using System.Collections.Concurrent;
using System.Net;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Tracks internal connection attempts and builds a user agent identifier.
    /// </summary>
    public class InternalConnectionService
    {
        private ConcurrentDictionary<string, DateTime> _connectionAttempts = new ConcurrentDictionary<string, DateTime>();
        /// <summary>
        /// Gets or sets the connection identifier for the current instance.
        /// </summary>
        protected long ConnectionId { get; set; } = DateTime.UtcNow.Ticks;

        /// <summary>
        /// Gets a user agent string for the current server instance.
        /// </summary>
        /// <returns>The user agent string.</returns>
        public string GetUserAgent()
        {
            return $"VideoWebPlayer/1.0 Instance/{ConnectionId}";
        }

        /// <summary>
        /// Determines whether a remote address is allowed based on recent attempts.
        /// </summary>
        /// <param name="remoteIpAddress">The remote IP address.</param>
        /// <returns><c>true</c> if the address is allowed; otherwise <c>false</c>.</returns>
        public bool IsAllowed(IPAddress? remoteIpAddress)
        {
            var key = remoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(key))
                return false;
            if (_connectionAttempts.TryGetValue(key, out var lastAttempt))
            {
                // Check if the last attempt was within the last 5 minutes
                return (DateTime.UtcNow - lastAttempt).TotalMinutes < 5;
            }
            return false;
        }

        /// <summary>
        /// Records an allowed connection attempt for the provided address.
        /// </summary>
        /// <param name="remoteIpAddress">The remote IP address.</param>
        public void Allow(IPAddress? remoteIpAddress)
        {
            var key = remoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(key))
                return;
            _connectionAttempts[key] = DateTime.UtcNow;
        }
    }
}
