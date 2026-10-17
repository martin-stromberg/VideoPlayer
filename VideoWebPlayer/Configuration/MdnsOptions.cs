namespace VideoWebPlayer.Configuration
{
    /// <summary>
    /// Strongly typed configuration options for the mDNS/DNS-SD advertisement of the server.
    /// </summary>
    public sealed class MdnsOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the server announces itself via mDNS.
        /// This is the operator master switch; the effective state additionally requires the
        /// admin switch <c>Setup.MdnsAdvertisementEnabled</c> (conjunction).
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the service instance name shown in DNS-SD browses.
        /// </summary>
        public string InstanceName { get; set; } = "VideoWebPlayer";

        /// <summary>
        /// Gets or sets the announced service type, e.g. <c>_videowebplayer._tcp.local.</c>.
        /// </summary>
        public string ServiceType { get; set; } = "_videowebplayer._tcp.local.";

        /// <summary>
        /// Gets or sets the announced port override. <c>null</c> derives the port from the
        /// bound server addresses, <c>Kestrel:Endpoints:Http:Url</c>, <c>Host:Port</c> or 5000.
        /// </summary>
        public int? Port { get; set; }
    }
}
