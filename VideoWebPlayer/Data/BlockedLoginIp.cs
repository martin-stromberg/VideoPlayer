namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a blocked login IP entry.
    /// </summary>
    public class BlockedLoginIp
    {
        /// <summary>
        /// Gets or sets the blocked IP address.
        /// </summary>
        public string Ip { get; set; } = "";
        /// <summary>
        /// Gets or sets the timestamp when the IP was blocked.
        /// </summary>
        public DateTime BlockedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the number of failures recorded for the IP.
        /// </summary>
        public int Failures { get; set; }
    }
}